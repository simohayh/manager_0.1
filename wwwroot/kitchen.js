// Kitchen dashboard. Needs common.js, auth.js and session.js.

const ORDER_REFRESH_MS = 2500;
const TIMER_REFRESH_MS = 1000;
const DEFAULT_PREP_MINUTES = 15;

const PREP_TIME_OPTIONS = [
    { minutes: 5 }, { minutes: 10 }, { minutes: 15 }, { minutes: 20 },
    { minutes: 25 }, { minutes: 30 }, { minutes: 40 },
    { minutes: 60, label: '1hr', isLong: true }
];

// ---------- Loading + rendering ----------

// Order codes this screen has already seen (null until the first load, so a refresh doesn't beep for old orders).
let seenOrderCodes = null;

function checkForNewOrders(orders) {
    const codes = orders.map(o => normalizeOrder(o).code);
    if (seenOrderCodes !== null) {
        const fresh = codes.filter(c => !seenOrderCodes.has(c));
        if (fresh.length > 0) alertNewOrders(fresh.length, fresh.length === 1 ? 'NEW ORDER' : 'NEW ORDERS');
    }
    seenOrderCodes = new Set([...(seenOrderCodes || []), ...codes]);
}

function fetchKitchenOrders() {
    apiFetch('/api/kitchen/orders')
<<<<<<< HEAD
        .then(res => {
            if (!res.ok && res.status !== 401) setConnectionLost(true);   // server error
            return res.ok ? res.json() : null;                           // not logged in: auth.js shows the PIN box
        })
        .then(orders => {
            if (!orders) return;
            setConnectionLost(false);
            checkForNewOrders(orders);
=======
        .then(res => (res.ok ? res.json() : null))   // not logged in: auth.js shows the PIN box
        .then(orders => {
            if (!orders) return;
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c

            const grid = byId('kitchen-grid');
            grid.innerHTML = '';

            if (!orders || orders.length === 0) {
                grid.innerHTML = emptyStateHtml();
                return;
            }

            orders.forEach(o => grid.appendChild(buildKitchenCard(o)));
            updateKitchenTimers();
        })
        .catch(err => { setConnectionLost(true); console.error("Kitchen fetch error:", err); });
}

function emptyStateHtml() {
    return `
        <div class="kitchen-empty">
            <h3>All Caught Up! 🎉</h3>
            <p>No active kitchen orders right now. The kitchen is completely clear.</p>
        </div>`;
}

function buildKitchenCard(o) {
    const order = normalizeOrder(o);
    const prepMinutes = order.prepMinutes || DEFAULT_PREP_MINUTES;
    const isPaid = o.isPaid ?? o.IsPaid ?? false;

    const imageHtml = order.imageUrl ? `<img src="${escapeHtml(order.imageUrl)}" alt="${escapeHtml(order.itemName)}" />` : '';
    const paymentBadge = isPaid
        ? `<span class="pay-badge pay-badge--paid">PAID</span>`
        : `<span class="pay-badge pay-badge--unpaid">UNPAID</span>`;
    const notesHtml = order.notes.trim()
        ? `<div class="kc-note"><strong>📝 Note:</strong> <span>${escapeHtml(order.notes)}</span></div>`
        : '';

    const card = document.createElement('div');
    card.className = 'kitchen-card';
    card.innerHTML = `
        <div>
            ${imageHtml}
            <div class="kc-head">
                <strong class="kc-code">${escapeHtml(order.code)}</strong>
                <div class="kc-badges">
                    ${paymentBadge}
                    <span class="badge-status status-${escapeHtml(order.status.toLowerCase())}">${escapeHtml(order.status)}</span>
                </div>
            </div>
            <div class="kc-item">${escapeHtml(order.quantity)}x ${escapeHtml(order.itemName)}</div>
            <div class="kc-table">📍 Table: <span>Table ${escapeHtml(order.table)}</span></div>
            ${notesHtml}
        </div>
        <div>${actionButtonsHtml(order, prepMinutes)}</div>
    `;
    return card;
}

function actionButtonsHtml(order, prepMinutes) {
    switch (order.status) {
        case 'Pending': return pendingActionsHtml(order.code);
        case 'Cooking': return cookingActionsHtml(order, prepMinutes);
        case 'Ready':   return readyActionsHtml(order.code);
        default:        return '';
    }
}

function pendingActionsHtml(code) {
    const timeButtons = PREP_TIME_OPTIONS.map(({ minutes, label, isLong }) => `
        <button class="btn-time${isLong ? ' btn-time--long' : ''}" data-action="start" data-code="${escapeHtml(code)}" data-minutes="${Number(minutes)}">${label || minutes + 'm'}</button>
    `).join('');

    return `
        <div class="prep-picker">
            <div class="prep-picker-label">Select Prep Time & Start:</div>
            <div class="time-btn-grid">${timeButtons}</div>
            <button class="btn-kitchen-action btn-out-of-stock" data-action="out-of-stock" data-code="${escapeHtml(code)}">Out of Stock ❌</button>
        </div>
    `;
}

function cookingActionsHtml(order, prepMinutes) {
    return `
        <div class="cooking-box">
            🍳 Cooking (${prepMinutes}m target)
            <div id="kitchen-timer-${escapeHtml(order.code)}" class="kitchen-timer" data-started="${escapeHtml(order.cookingStartedAt)}" data-prep="${Number(prepMinutes)}">Calculating timer...</div>
        </div>
        <div class="action-row">
            <button class="btn-kitchen-action btn-undo" data-action="status" data-status="Pending" data-code="${escapeHtml(order.code)}">⬅ Undo</button>
            <button class="btn-kitchen-action btn-ready" data-action="status" data-status="Ready" data-code="${escapeHtml(order.code)}">✓ Mark Ready 🔔</button>
        </div>
        <button class="btn-kitchen-action btn-out-of-stock" data-action="out-of-stock" data-code="${escapeHtml(order.code)}">Out of Stock ❌</button>
    `;
}

function readyActionsHtml(code) {
    return `
        <div class="action-row action-row--ready">
            <button class="btn-kitchen-action btn-undo" data-action="status" data-status="Cooking" data-code="${escapeHtml(code)}">⬅ Undo</button>
            <div class="ready-label">Ready For Delivery</div>
        </div>
    `;
}

// ---------- Countdown timers (updated in place every second) ----------

function updateKitchenTimers() {
    document.querySelectorAll('[id^="kitchen-timer-"]').forEach(el => {
        const startedAt = el.getAttribute('data-started');
        const prepMins = parseInt(el.getAttribute('data-prep')) || DEFAULT_PREP_MINUTES;

        if (!startedAt) {
            el.innerText = `⏱ ${prepMins}m allocated`;
            return;
        }

        const remainingMs = prepMins * 60 * 1000 - (Date.now() - parseUtcTime(startedAt));

        if (remainingMs > 0) {
            el.innerText = `⏱ ${formatMinSec(remainingMs)} remaining`;
            el.style.color = "#ffd54f";
        } else {
            el.innerText = `⚠️ Overdue by ${formatMinSec(Math.abs(remainingMs))}`;
            el.style.color = "#ff5252";
        }
    });
}

// ---------- Actions ----------

function sendKitchenAction(url, payload) {
    postJson(url, payload).then(() => fetchKitchenOrders()).catch(() => fetchKitchenOrders());
}

/** One click listener for every button on the cards (they carry data-action / data-code / ...). */
function handleKitchenClick(event) {
    const btn = event.target.closest('[data-action]');
    if (!btn) return;

    const code = btn.dataset.code;
    switch (btn.dataset.action) {
        case 'start':        startCookingWithTime(code, Number(btn.dataset.minutes)); break;
        case 'out-of-stock': markOutOfStock(code); break;
        case 'status':       updateOrderStatus(code, btn.dataset.status); break;
    }
}

function startCookingWithTime(code, minutes) {
    sendKitchenAction('/api/kitchen/start-cooking', { orderCode: code, prepMinutes: minutes });
}

function markOutOfStock(code) {
    sendKitchenAction('/api/kitchen/out-of-stock', { orderCode: code });
}

function updateOrderStatus(code, newStatus) {
    sendKitchenAction('/api/kitchen/update-status', { orderCode: code, newStatus });
}

// ---------- Start-up ----------

document.addEventListener('DOMContentLoaded', () => {
    byId('kitchen-grid').addEventListener('click', handleKitchenClick);

    initStaffAuth('kitchen', fetchKitchenOrders);

    startDeviceSession({
        role: 'kitchen',
        idPrefix: 'KDEV-',
        logoutTitle: 'Kitchen Session Ended',
        logoutHint: 'You may now safely close this browser tab.'
    });

    fetchKitchenOrders();
    setInterval(fetchKitchenOrders, ORDER_REFRESH_MS);
    setInterval(updateKitchenTimers, TIMER_REFRESH_MS);
});
