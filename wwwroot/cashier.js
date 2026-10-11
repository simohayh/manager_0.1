// Cashier dashboard. Needs common.js, auth.js and session.js.

const ORDER_REFRESH_MS = 2500;
const TOAST_DURATION_MS = 3500;

// ---------- Loading + rendering ----------

function fetchCashierOrders() {
    apiFetch('/api/cashier/orders')
        .then(res => (res.ok ? res.json() : null))   // not logged in: auth.js shows the PIN box
        .then(rawOrders => {
            if (!rawOrders) return;

            const grid = byId('cashier-grid');
            if (!grid) return;
            grid.innerHTML = '';

            if (rawOrders.length === 0) {
                grid.innerHTML = `<div class="empty-feed">No active orders in the restaurant right now.</div>`;
                return;
            }

            const ordersByTable = groupByTable(rawOrders.map(normalizeOrder));
            for (const [tableNum, tableOrders] of Object.entries(ordersByTable)) {
                grid.appendChild(buildTableCard(tableNum, tableOrders));
            }
        })
        .catch(err => console.error("Error fetching cashier orders:", err));
}

// A plain object (not a Map) on purpose: numeric table numbers then list in ascending order.
function groupByTable(orders) {
    const tables = {};
    orders.forEach(order => {
        if (!tables[order.table]) tables[order.table] = [];
        tables[order.table].push(order);
    });
    return tables;
}

function buildTableCard(tableNum, orders) {
    let tableTotal = 0;
    let paymentStatus = 'Unpaid';
    let sharedToken = '';

    for (const order of orders) {
        tableTotal += order.price * order.quantity;
        paymentStatus = order.paymentStatus;
        if (order.paymentToken) sharedToken = order.paymentToken;
    }

    const tokenBox = sharedToken ? `<div class="token-display">Pass Token: ${escapeHtml(sharedToken)}</div>` : '';

    const card = document.createElement('div');
    card.className = 'table-card';
    card.innerHTML = `
        <div class="table-body">
            <div class="table-header">
                <span class="table-title">Table ${escapeHtml(tableNum)}</span>
                ${paymentBadgeHtml(paymentStatus)}
            </div>
            <div>${orders.map(orderRowHtml).join('')}</div>
        </div>
        <div class="table-footer">
            ${tokenBox}
            <div class="total-row">
                <span>Total Due:</span>
                <span>${money(tableTotal)}</span>
            </div>
            ${settleActionHtml(paymentStatus, tableNum)}
        </div>
    `;
    return card;
}

function orderRowHtml(order) {
    return `
        <div class="order-item-row">
            <div>
                <strong>${escapeHtml(order.quantity)}x ${escapeHtml(order.itemName)}</strong>
                <div class="order-code">Code: ${escapeHtml(order.code)}</div>
            </div>
            <div class="order-item-meta">
                <span class="badge status-${escapeHtml(order.status.toLowerCase())}">${escapeHtml(order.status)}</span>
<<<<<<< HEAD
                <span class="order-item-price">${money(order.price * order.quantity)}</span>
=======
                <span class="order-item-price">$${(order.price * order.quantity).toFixed(2)}</span>
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
            </div>
        </div>
    `;
}

function paymentBadgeHtml(paymentStatus) {
    if (paymentStatus === 'AwaitingVerification') return `<span class="badge payment-verifying">AWAITING VERIFY</span>`;
    if (paymentStatus === 'Paid') return `<span class="badge payment-paid">PAID</span>`;
    return `<span class="badge payment-unpaid">UNPAID</span>`;
}

function settleActionHtml(paymentStatus, tableNum) {
    if (paymentStatus === 'AwaitingVerification') {
        return `<button class="btn-settle" data-table="${escapeHtml(tableNum)}">Verify Code & Settle</button>`;
    }
    if (paymentStatus === 'Paid') {
        return `<div class="settle-note settle-note--done">✓ Settled & Verified</div>`;
    }
    return `<div class="settle-note settle-note--waiting">Awaiting customer payment request</div>`;
}

// ---------- Actions ----------

function settleTableBill(tableQuery) {
    postJson('/api/cashier/settle', { query: tableQuery })
        .then(res => res.json().catch(() => ({})))
        .then(data => {
            if (data.success) {
                showToast(`Successfully verified and settled Table ${tableQuery}!`);
                fetchCashierOrders();
            } else {
                showToast(data.message || "Settlement failed.", true);
            }
        })
        .catch(() => showToast("Failed to settle table bill.", true));
}

/** One click listener for every settle button (they carry the table in data-table). */
function handleCashierClick(event) {
    const btn = event.target.closest('.btn-settle');
    if (btn) settleTableBill(btn.dataset.table);
}

function showToast(message, isWarning = false) {
    const toast = document.createElement('div');
    toast.className = 'toast';
    if (isWarning) toast.style.background = '#e53935';
    toast.innerText = message;

    byId('toast-container').appendChild(toast);
    setTimeout(() => toast.remove(), TOAST_DURATION_MS);
}

// ---------- Start-up ----------

document.addEventListener('DOMContentLoaded', () => {
<<<<<<< HEAD
    loadConfig();
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
    byId('cashier-grid').addEventListener('click', handleCashierClick);

    initStaffAuth('cashier', fetchCashierOrders);

    startDeviceSession({
        role: 'cashier',
        idPrefix: 'DEV-',
        logoutTitle: 'Device Logged Out Successfully',
        logoutHint: 'You may now close this window or tab.',
        onClaimed: () => showToast("Successfully took over session!")
    });

    fetchCashierOrders();
    setInterval(fetchCashierOrders, ORDER_REFRESH_MS);
});
