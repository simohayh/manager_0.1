// Customer page: menu, cart, order tracking and payment. Needs common.js.

// ===== Constants =====
const CART_KEY = 'cart_items';
const ORDERS_KEY = 'my_orders';

const ORDERS_POLL_MS = 2500;        // refresh of the "My Orders" tab
const OUT_OF_STOCK_POLL_MS = 3000;  // check for items the kitchen cancelled
const TIMER_TICK_MS = 1000;
const TOAST_DURATION_MS = 4000;
const DEFAULT_PREP_MINUTES = 15;

const PLACEHOLDER_MENU_IMAGE = 'https://via.placeholder.com/300x180';
const PLACEHOLDER_CART_IMAGE = 'https://via.placeholder.com/60';

// ===== State =====
let currentCategory = 'All';
let currentDietary = 'All';
let selectedItem = null;
let autoRefreshInterval = null;
let currentAlertedOrderCode = null;
const previousOrderStatuses = {};

// ===== Small helpers =====
function setText(id, value) {
    const el = byId(id);
    if (el) el.innerText = value;
}

function stopAutoRefresh() {
    clearInterval(autoRefreshInterval);
}

function showToast(message, isWarning = false) {
    let container = byId('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = isWarning ? 'toast toast-warning' : 'toast';
    toast.innerHTML = `<span>${isWarning ? '⚠️' : '✓'}</span> ${escapeHtml(message)}`;
    container.appendChild(toast);

    setTimeout(() => toast.remove(), TOAST_DURATION_MS);
}

function playBellSound() {
    try {
        const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        const oscillator = audioCtx.createOscillator();
        const gainNode = audioCtx.createGain();
        const now = audioCtx.currentTime;

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(880, now);
        oscillator.frequency.exponentialRampToValueAtTime(440, now + 0.8);

        gainNode.gain.setValueAtTime(0.3, now);
        gainNode.gain.exponentialRampToValueAtTime(0.001, now + 0.8);

        oscillator.connect(gainNode);
        gainNode.connect(audioCtx.destination);

        oscillator.start();
        oscillator.stop(now + 0.8);
    } catch (e) {
        console.log("Audio Context blocked or not supported", e);
    }
}

/** Opens a full-screen dialog (replacing any dialog with the same id) and returns its overlay element. */
function showDialog(id, variant, innerHtml) {
    const existing = byId(id);
    if (existing) existing.remove();

    const overlay = document.createElement('div');
    overlay.id = id;
    overlay.className = 'dialog-overlay';
    overlay.innerHTML = `<div class="dialog-card dialog-card--${variant}">${innerHtml}</div>`;
    document.body.appendChild(overlay);
    return overlay;
}

// ===== Local storage: cart + saved order codes =====
function readList(key) {
    try {
        return JSON.parse(localStorage.getItem(key) || '[]');
    } catch (e) {
        return [];
    }
}

function getCart() {
    return readList(CART_KEY);
}

function saveCart(cart) {
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    updateCartBadge();
}

function updateCartBadge() {
    const totalCount = getCart().reduce((sum, item) => sum + item.quantity, 0);
    setText('cart-count', totalCount);
}

function getSavedOrderCodes() {
    return readList(ORDERS_KEY);
}

function saveOrderCode(code) {
    const codes = getSavedOrderCodes();
    if (!codes.includes(code)) {
        codes.push(code);
        localStorage.setItem(ORDERS_KEY, JSON.stringify(codes));
    }
    updateOrdersBadge();
}

function updateOrdersBadge() {
    setText('orders-count', getSavedOrderCodes().length);
}

// ===== Tabs / views =====
/** Shows one of: 'menu', 'cart', 'orders' and highlights the clicked tab button. */
function showView(view, btn, activeClass = 'active') {
    byId('search-bar-container').style.display = view === 'menu' ? 'block' : 'none';
    byId('menu-grid').style.display = view === 'menu' ? 'grid' : 'none';
    byId('cart-grid').style.display = view === 'cart' ? 'grid' : 'none';
    byId('my-orders-grid').style.display = view === 'orders' ? 'grid' : 'none';

    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active', 'vegan-active'));
    btn.classList.add(activeClass);
}

function setCategory(cat, btn) {
    currentCategory = cat;
    currentDietary = 'All';

    showView('menu', btn);
    stopAutoRefresh();
    fetchMenu();
}

function setDietary(diet, btn) {
    currentDietary = diet;
    currentCategory = 'All';

    showView('menu', btn, 'vegan-active');
    stopAutoRefresh();
    fetchMenu();
}

function showCartView(btn) {
    showView('cart', btn);
    renderCart();
    stopAutoRefresh();
}

function showMyOrders(btn) {
    showView('orders', btn);
    fetchMyOrders();
    stopAutoRefresh();
    autoRefreshInterval = setInterval(fetchMyOrders, ORDERS_POLL_MS);
}

// ===== Menu =====
function fetchMenu() {
    const searchInput = byId('search-input');
    const search = searchInput ? searchInput.value : '';
    const url = `/api/menu?search=${encodeURIComponent(search)}&category=${encodeURIComponent(currentCategory)}&dietary=${encodeURIComponent(currentDietary)}`;

    fetch(url)
        .then(res => res.json())
        .then(items => {
            const grid = byId('menu-grid');
            if (!grid) return;
            grid.innerHTML = '';
            items.forEach(item => grid.appendChild(buildMenuCard(item)));
        });
}

function buildMenuCard(item) {
    const menuItem = {
        id: item.id || item.Id || 1,
        name: item.name || item.Name || '',
        price: item.price ?? item.Price ?? 0,
        description: item.description || item.Description || '',
        dietary: item.dietary || item.Dietary || '',
        category: item.category || item.Category || '',
        imageUrl: item.imageUrl || item.ImageUrl || PLACEHOLDER_MENU_IMAGE
    };
    const dietaryBadgeClass = menuItem.dietary === 'Vegan' ? 'badge-vegan' : 'badge-nonvegan';

    const card = document.createElement('div');
    card.className = 'card';
    card.onclick = () => openOrderModal(menuItem);
    card.innerHTML = `
        <div>
            <img src="${escapeHtml(menuItem.imageUrl)}" alt="${escapeHtml(menuItem.name)}" class="card-img" />
            <div class="card-header">
                <div class="card-title">${escapeHtml(menuItem.name)}</div>
<<<<<<< HEAD
                <div class="card-price">${money(menuItem.price)}</div>
=======
                <div class="card-price">$${Number(menuItem.price).toFixed(2)}</div>
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
            </div>
            <div class="card-desc">${escapeHtml(menuItem.description)}</div>
        </div>
        <div class="badge-container">
            <span class="badge ${dietaryBadgeClass}">${escapeHtml(menuItem.dietary)}</span>
            <span class="badge badge-cat">${escapeHtml(menuItem.category)}</span>
        </div>
    `;
    return card;
}

// ===== "Add to cart" modal =====
function openOrderModal(item) {
    selectedItem = item;
    byId('modal-item-name').innerText = item.name;
    byId('item-qty').value = 1;

    const notesInput = byId('item-notes');
    if (notesInput) notesInput.value = '';

    byId('order-modal').style.display = 'flex';
}

function closeModal() {
    byId('order-modal').style.display = 'none';
}

function addToCart() {
    const qty = parseInt(byId('item-qty').value) || 1;
    const notesInput = byId('item-notes');
    const notes = notesInput ? notesInput.value.trim() : '';
    const cart = getCart();

    // Same item with the same note just increases the quantity.
    const existing = cart.find(cartItem =>
        cartItem.name === selectedItem.name && (cartItem.notes || '') === notes
    );

    if (existing) {
        existing.quantity += qty;
    } else {
        cart.push({
            menuItemId: selectedItem.id,
            name: selectedItem.name,
            price: selectedItem.price,
            quantity: qty,
            imageUrl: selectedItem.imageUrl,
            notes: notes
        });
    }

    saveCart(cart);
    closeModal();
    showToast(`Added ${qty}x ${selectedItem.name} to your cart!`);
}

// ===== Cart =====
function removeFromCart(index) {
    const cart = getCart();
    cart.splice(index, 1);
    saveCart(cart);
    renderCart();
}

function renderCart() {
    const cart = getCart();
    const grid = byId('cart-grid');
    if (!grid) return;
    grid.innerHTML = '';

    if (cart.length === 0) {
        grid.innerHTML = `<div class="empty-state">Your cart is empty. Pick some delicious items from the menu!</div>`;
        return;
    }

    const cartTotal = cart.reduce((sum, item) => sum + item.price * item.quantity, 0);

    const checkoutCard = document.createElement('div');
    checkoutCard.className = 'my-order-card cart-card';
    checkoutCard.innerHTML = `
        <h3 class="cart-title">Review Your Cart</h3>
        <div class="cart-items">${cart.map(cartRowHtml).join('')}</div>
        <div class="cart-total">
            Total Cart Value: <span>${money(cartTotal)}</span>
        </div>

        <div class="cart-table-field">
            <label>Table Number</label>
            <input type="text" id="cart-table-num" class="cart-table-input" placeholder="e.g. Table 5">
        </div>
        <button class="btn-submit btn-place-order" onclick="submitFinalCartOrder()">Place Order (Pay After Eating)</button>
    `;
    grid.appendChild(checkoutCard);
}

function cartRowHtml(item, index) {
    const img = item.imageUrl || PLACEHOLDER_CART_IMAGE;
    const noteHtml = item.notes ? `<div class="cart-row-note">📝 Note: ${escapeHtml(item.notes)}</div>` : '';

    return `
        <div class="cart-row">
            <img src="${escapeHtml(img)}" alt="${escapeHtml(item.name)}" class="cart-row-img" />
            <div class="cart-row-info">
                <strong>${escapeHtml(item.quantity)}x ${escapeHtml(item.name)}</strong>
<<<<<<< HEAD
                <div class="cart-row-price">${money(item.price)} each</div>
                ${noteHtml}
            </div>
            <div class="cart-row-actions">
                <span>${money(item.price * item.quantity)}</span>
=======
                <div class="cart-row-price">$${Number(item.price).toFixed(2)} each</div>
                ${noteHtml}
            </div>
            <div class="cart-row-actions">
                <span>$${(item.price * item.quantity).toFixed(2)}</span>
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
                <button class="cart-remove-btn" onclick="removeFromCart(${Number(index)})">✕</button>
            </div>
        </div>
    `;
}

function flagInvalidTableInput(tableInput) {
    if (!tableInput) return;
    tableInput.classList.add('input-error');
    tableInput.focus();
}

async function submitFinalCartOrder() {
    const cart = getCart();
    const tableInput = byId('cart-table-num');
    const table = tableInput ? tableInput.value.trim() : '';

    if (!table) {
        flagInvalidTableInput(tableInput);
        showToast("Please enter your table number before placing the order.", true);
        return;
    }
    if (tableInput) tableInput.classList.remove('input-error');

    if (cart.length === 0) {
        showToast("Your cart is empty.", true);
        return;
    }

    try {
        // 1. Check the table number is valid.
        // (The server checks again when the order is placed; this just gives a friendlier early message.)
        const validationRes = await fetch(`/api/tables/validate?tableNumber=${encodeURIComponent(table.replace(/^table\s*/i, ''))}`);
        const validation = await validationRes.json().catch(() => null);
        if (!validationRes.ok || (validation && validation.success === false)) {
            flagInvalidTableInput(tableInput);
            showToast((validation && validation.message) || "Invalid or inactive table number. Please check and try again.", true);
            return;
        }

        // 2. Place all cart items as one batch.
        const res = await postJson('/api/orders/batch', {
            tableNumber: table,
            items: cart.map(item => ({
                menuItemId: item.menuItemId || 1,
                quantity: item.quantity,
                notes: item.notes || ''
            })),
            paymentToken: null
        });

        const text = await res.text();
        let data;
        try {
            data = JSON.parse(text);
        } catch (e) {
            throw new Error(text || "Server returned an invalid response.");
        }

        // 3. Success: remember the order codes and switch to "My Orders".
        if (data.orderCodes) {
            data.orderCodes.forEach(saveOrderCode);
            localStorage.removeItem(CART_KEY);
            updateCartBadge();
            showToast("All items successfully ordered and sent to the kitchen!");
            showMyOrders(byId('my-orders-btn'));
        } else {
            showToast(data.message || "Failed to place order.", true);
        }
    } catch (err) {
        if (!err.message?.includes("Failed to fetch")) {
            showToast("Error placing cart orders: " + err.message, true);
        }
    }
}

// ===== My Orders =====
// Cards are updated in place (not rebuilt) so countdowns and buttons don't flicker.
function fetchMyOrders() {
    const codes = getSavedOrderCodes();
    const grid = byId('my-orders-grid');
    const timerSection = byId('customer-timer-section');
    if (!grid) return;

    if (codes.length === 0) {
        grid.innerHTML = `<div class="empty-state">No active orders tracked right now.</div>`;
        if (timerSection) timerSection.style.display = 'none';
        return;
    }

    postJson('/api/orders/track-batch', { codes })
        .then(res => res.json())
        .then(rawOrders => {
            const orders = (rawOrders || []).map(normalizeOrder);

            // Orders gone from the server, or all settled: the table session is over.
            const allSettled = orders.every(o => o.status === 'Completed' || o.paymentStatus === 'Paid');
            if (orders.length === 0 || allSettled) {
                if (timerSection) timerSection.style.display = 'none';
                showSettledConfirmationModal();
                return;
            }

            notifyNewlyReadyOrders(orders);

            const anyCooking = orders.some(o => o.status === 'Cooking');
            if (timerSection) timerSection.style.display = anyCooking ? 'block' : 'none';

            renderSummaryCard(grid, orders);

            const placeholder = grid.querySelector('.empty-state');
            if (placeholder) placeholder.remove();

            syncOrderCards(grid, orders);
        })
        .catch(err => console.error("Error fetching orders:", err));
}

function notifyNewlyReadyOrders(orders) {
    orders.forEach(order => {
        const previous = previousOrderStatuses[order.code];
        if (previous && previous !== 'Ready' && order.status === 'Ready') {
            playBellSound();
            showToast(`🔔 Ding-Dong! Your order ${order.code} is ready!`);
        }
        previousOrderStatuses[order.code] = order.status;
    });
}

function renderSummaryCard(grid, orders) {
    // The payment state shown is the last order's; the token is the last non-empty one.
    let totalExpense = 0;
    let paymentStatus = 'Unpaid';
    let paymentToken = '';

    orders.forEach(order => {
        totalExpense += order.price * order.quantity;
        paymentStatus = order.paymentStatus;
        if (order.paymentToken) paymentToken = order.paymentToken;
    });

    let summaryCard = byId('orders-summary-card');
    if (!summaryCard) {
        summaryCard = document.createElement('div');
        summaryCard.id = 'orders-summary-card';
        summaryCard.className = 'orders-summary-card';
        grid.insertBefore(summaryCard, grid.firstChild);
    }

    let actionHtml = '';
    if (paymentStatus === 'Unpaid') {
        actionHtml = `<button class="btn-request-payment" onclick="handlePaymentClick()">💳 Request Payment / Pay Cash</button>`;
    } else if (paymentStatus === 'AwaitingVerification') {
        actionHtml = `<span class="pill-awaiting">⏳ Awaiting Verification from Cashier...</span>`;
    }

    const tokenHtml = paymentToken
        ? `<div class="pass-token">🔐 Pass Token: ${escapeHtml(paymentToken)} (Show this to Cashier)</div>`
        : '';

    summaryCard.innerHTML = `
        <div class="summary-main">
            <strong class="summary-title">Session Bill Summary</strong>
            <span class="summary-meta">Active items: ${orders.length} | Total Due: <span class="summary-total">${money(totalExpense)}</span></span>
            <div>${tokenHtml}</div>
        </div>
        <div class="summary-actions">${actionHtml}</div>
    `;
}

function syncOrderCards(grid, orders) {
    const activeCodes = new Set();

    orders.forEach(order => {
        activeCodes.add(order.code);

        const card = byId(`order-card-${order.code}`);
        if (card) {
            updateOrderCard(card, order);
        } else {
            grid.appendChild(buildOrderCard(order));
        }
    });

    // Remove cards for orders that no longer exist.
    document.querySelectorAll('[id^="order-card-"]').forEach(card => {
        const code = card.id.replace('order-card-', '');
        if (!activeCodes.has(code)) card.remove();
    });
}

/** Changes whenever something visible on the card needs re-rendering. */
function orderSignature(order) {
    return [order.status, order.paymentStatus, order.prepMinutes ?? '', order.cookingStartedAt].join('|');
}

function buildOrderCard(order) {
    const itemTotal = order.price * order.quantity;
    const notesHtml = order.notes
        ? `<p class="order-note"><strong>Note:</strong> ${escapeHtml(order.notes)}</p>`
        : '';

    const card = document.createElement('div');
    card.id = `order-card-${order.code}`; // codes are server-made (letters, digits, "-"), so safe as an element id
    card.className = 'my-order-card';
    card.setAttribute('data-signature', orderSignature(order));
    card.innerHTML = `
        <div class="order-card-head">
            <span class="order-card-code">${escapeHtml(order.code)}</span>
            <div class="order-card-badges">
                <span class="payment-badge-slot">${paymentBadgeHtml(order.paymentStatus)}</span>
                <span class="status-badge-slot badge-status status-${escapeHtml(order.status.toLowerCase())}">${escapeHtml(order.status)}</span>
            </div>
        </div>
        <p class="order-line"><strong>Item:</strong> ${escapeHtml(order.quantity)}x ${escapeHtml(order.itemName)}</p>
        ${notesHtml}
<<<<<<< HEAD
        <p class="order-line"><strong>Cost:</strong> ${money(order.price)} × ${escapeHtml(order.quantity)} = <strong>${money(itemTotal)}</strong></p>
=======
        <p class="order-line"><strong>Cost:</strong> $${order.price.toFixed(2)} × ${order.quantity} = <strong>$${itemTotal.toFixed(2)}</strong></p>
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        <p class="order-line"><strong>Table:</strong> ${escapeHtml(order.table)}</p>
        <div class="action-area-slot">${orderActionHtml(order)}</div>
    `;
    return card;
}

function updateOrderCard(card, order) {
    const signature = orderSignature(order);
    if (card.getAttribute('data-signature') === signature) return;

    card.setAttribute('data-signature', signature);
    card.querySelector('.payment-badge-slot').innerHTML = paymentBadgeHtml(order.paymentStatus);

    const statusBadge = card.querySelector('.status-badge-slot');
    statusBadge.className = `status-badge-slot badge-status status-${order.status.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
    statusBadge.innerText = order.status;

    card.querySelector('.action-area-slot').innerHTML = orderActionHtml(order);
}

function paymentBadgeHtml(paymentStatus) {
    return paymentStatus === 'AwaitingVerification'
        ? `<span class="pay-badge pay-badge--verifying">VERIFYING</span>`
        : `<span class="pay-badge pay-badge--unpaid">UNPAID</span>`;
}

function orderActionHtml(order) {
    if (order.status === 'Ready') {
        return `<button class="btn-submit btn-confirm-delivered" data-code="${escapeHtml(order.code)}">Confirm Delivered</button>`;
    }

    if (order.status === 'Delivered') {
        return `<p class="order-delivered">✓ Delivered & Completed</p>`;
    }

    if (order.status === 'Cooking' && order.prepMinutes) {
        return `
            <div class="cooking-progress">
                <div class="cooking-progress-head">
                    <span>⏱ Cooking (${Number(order.prepMinutes)}m target)</span>
                    <span id="cust-countdown-${escapeHtml(order.code)}" data-started="${escapeHtml(order.cookingStartedAt)}" data-prep="${Number(order.prepMinutes)}">Calculating...</span>
                </div>
                <div class="countdown-progress-container">
                    <div id="cust-progress-${escapeHtml(order.code)}" class="countdown-progress-bar"></div>
                </div>
            </div>
        `;
    }

    return `<p class="order-hint">⏱ Waiting for kitchen preparation...</p>`;
}

// ===== Countdown timers (updated in place every second) =====
function updateCountdowns() {
    let maxRemainingMs = 0;
    let maxTotalMs = 1;

    document.querySelectorAll('[id^="cust-countdown-"]').forEach(el => {
        const code = el.id.replace('cust-countdown-', '');
        const startedAt = el.getAttribute('data-started');
        if (!startedAt) return;

        const prepMins = parseInt(el.getAttribute('data-prep')) || DEFAULT_PREP_MINUTES;
        const totalMs = prepMins * 60 * 1000;
        const timeLeftMs = Math.max(0, totalMs - (Date.now() - parseUtcTime(startedAt)));

        // The header bar follows the order with the most time left.
        if (timeLeftMs > maxRemainingMs) {
            maxRemainingMs = timeLeftMs;
            maxTotalMs = totalMs;
        }

        const progressBar = byId(`cust-progress-${code}`);
        if (progressBar) progressBar.style.width = `${toPercent(timeLeftMs, totalMs)}%`;

        el.innerText = timeLeftMs === 0 ? "Plating your order!" : `${formatMinSec(timeLeftMs)} remaining`;
    });

    const headerFill = byId('customer-progress-fill');
    const headerText = byId('customer-countdown-text');
    if (headerFill && headerText) {
        headerFill.style.width = `${toPercent(maxRemainingMs, maxTotalMs)}%`;
        headerText.innerText = maxRemainingMs === 0
            ? "Plating your order..."
            : `${formatMinSec(maxRemainingMs)} left`;
    }
}

function toPercent(part, total) {
    return Math.max(0, Math.min(100, (part / total) * 100));
}

// ===== Payment =====
function handlePaymentClick() {
    const codes = getSavedOrderCodes();
    if (codes.length === 0) {
        showToast("No active orders.", true);
        return;
    }

    postJson('/api/orders/track-batch', { codes })
        .then(res => res.json())
        .then(orders => {
            if (!Array.isArray(orders)) return;
            const allDelivered = orders.every(o => (o.status || o.Status) === 'Delivered');
            if (allDelivered) {
                showPaymentWarningModal();
            } else {
                showPaymentBlockedModal();
            }
        });
}

function showPaymentBlockedModal() {
    const overlay = showDialog('custom-warning-modal', 'danger', `
        <div class="dialog-icon">🛑</div>
        <h3 class="dialog-title">Delivery Incomplete</h3>
        <p class="dialog-text">You cannot request payment yet because some of your ordered items have not been fully marked as delivered to your table.</p>
        <button id="close-warning-btn" class="dialog-btn">Got It</button>
    `);

    byId('close-warning-btn').onclick = () => overlay.remove();
}

function showPaymentWarningModal() {
    const overlay = showDialog('custom-warning-modal', 'warning', `
        <div class="dialog-icon">⚠️</div>
        <h3 class="dialog-title">Payment & Session Notice</h3>
        <p class="dialog-text">
            After paying, any subsequent order will be considered a <strong>new set of orders</strong> for a <strong>new customer</strong> at this table.
            <br><br>
            <span class="dialog-info">
                ℹ️ The software treats this as your previous session being settled and closed, ensuring incoming orders from future patrons do not merge with your history.
            </span>
        </p>
        <div class="dialog-actions">
            <button id="cancel-pay-btn" class="dialog-btn dialog-btn--cancel">Cancel</button>
            <button id="proceed-pay-btn" class="dialog-btn dialog-btn--proceed">Proceed to Pay</button>
        </div>
    `);

    byId('cancel-pay-btn').onclick = () => overlay.remove();
    byId('proceed-pay-btn').onclick = () => {
        overlay.remove();
        executeRequestPayment();
    };
}

function executeRequestPayment() {
    postJson('/api/orders/request-payment', { codes: getSavedOrderCodes() })
        .then(res => res.json().catch(() => ({})).then(data => ({ ok: res.ok, data })))
        .then(({ ok, data }) => {
            fetchMyOrders();
            if (!ok) {
                showToast(data.message || "Couldn't request payment. Please ask a member of staff.", true);
                return;
            }
            const token = data.paymentToken || data.PaymentToken || 'N/A';
            showToast(`Payment requested! Show token ${token} to cashier ⏳`);
        })
        .catch(err => showToast("Failed to request payment: " + err, true));
}

function showSettledConfirmationModal() {
    const overlay = showDialog('settled-confirm-modal', 'success', `
        <div class="dialog-icon">🎉</div>
        <h3 class="dialog-title">Bill Settled Successfully!</h3>
        <p class="dialog-text">The cashier has verified and settled your payment. Your table session is now closed. Thank you for dining with us!</p>
        <button id="close-settled-modal-btn" class="dialog-btn">Awesome, Thanks!</button>
    `);

    byId('close-settled-modal-btn').onclick = () => {
        overlay.remove();
        localStorage.removeItem(ORDERS_KEY);
        localStorage.removeItem(CART_KEY);
        updateOrdersBadge();
        updateCartBadge();
        fetchMyOrders();
    };
}

function confirmCustomerDelivery(code) {
    // The server decides the new status itself (Ready -> Delivered); we only say which order.
    postJson('/api/orders/confirm-delivery', { orderCode: code })
        .then(res => res.json().catch(() => ({})).then(data => ({ ok: res.ok, data })))
        .then(({ ok, data }) => {
            fetchMyOrders();
            if (ok) showToast("Order marked as delivered!");
            else showToast(data.message || "Couldn't confirm delivery.", true);
        })
        .catch(() => showToast("Couldn't confirm delivery. Please try again.", true));
}

// ===== Out-of-stock alerts =====
function checkCustomerOrderStatus() {
    const codes = getSavedOrderCodes();
    if (codes.length === 0) return;

    postJson('/api/customer/orders', { codes })
        .then(res => res.json())
        .then(rawOrders => {
            if (!rawOrders) return;

            const outOfStock = rawOrders.map(normalizeOrder).find(o => o.status === 'Out of Stock');
            if (!outOfStock || currentAlertedOrderCode === outOfStock.code) return;

            currentAlertedOrderCode = outOfStock.code;
            byId('oosMessage').innerText =
                `We are very sorry, but "${outOfStock.itemName}" (${outOfStock.code}) is currently out of stock and has been removed from your active orders.`;
            byId('outOfStockModal').style.display = 'flex';
        })
        .catch(err => console.error("Customer order sync error:", err));
}

function dismissOutOfStockModal() {
    byId('outOfStockModal').style.display = 'none';
    if (!currentAlertedOrderCode) return;

    postJson('/api/customer/remove-order', { orderCode: currentAlertedOrderCode })
        .then(() => {
            currentAlertedOrderCode = null;
            checkCustomerOrderStatus();
        });
}

// ===== Start-up =====
setInterval(updateCountdowns, TIMER_TICK_MS);
setInterval(checkCustomerOrderStatus, OUT_OF_STOCK_POLL_MS);

document.addEventListener('DOMContentLoaded', () => {
    // Order-card buttons carry the order code in data-code (never inside inline JavaScript).
    byId('my-orders-grid').addEventListener('click', event => {
        const btn = event.target.closest('.btn-confirm-delivered');
        if (btn) confirmCustomerDelivery(btn.dataset.code);
    });

    updateCartBadge();
    updateOrdersBadge();
    loadConfig().then(fetchMenu); // prices need the currency symbol first
});
