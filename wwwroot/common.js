// Helpers shared by every page (customer, kitchen, cashier).

const byId = id => document.getElementById(id);

/**
 * Makes text safe to put inside HTML (element text or a quoted attribute value).
 * ALWAYS use this for anything a customer or the database could have supplied: notes, names, codes, table numbers...
 * It is NOT enough for JavaScript strings (e.g. inline onclick="..."); use data-* attributes for those.
 */
function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, ch => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
}

/**
 * fetch() that tells the staff pages when the login has expired (HTTP 401).
 * A page that has staff login defines onStaffUnauthorized(); the customer page doesn't, so nothing happens there.
 */
function apiFetch(url, options) {
    return fetch(url, options).then(res => {
        if (res.status === 401 && typeof onStaffUnauthorized === 'function') onStaffUnauthorized();
        return res;
    });
}

/** POST a JSON payload and return the fetch Response. */
function postJson(url, payload) {
    return apiFetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
}

/** .NET sends UTC timestamps without a "Z"; add it so the browser doesn't read them as local time. */
function parseUtcTime(dateStr) {
    if (!dateStr) return Date.now();

    let str = String(dateStr);
    if (!/[Z+]/.test(str)) str += 'Z';

    const parsed = new Date(str).getTime();
    return isNaN(parsed) ? Date.now() : parsed;
}

/** 125000 -> "2m 05s" */
function formatMinSec(ms) {
    const mins = Math.floor(ms / 60000);
    const secs = Math.floor((ms % 60000) / 1000);
    return `${mins}m ${secs < 10 ? '0' : ''}${secs}s`;
}

/** The API may send camelCase or PascalCase; return one consistent shape with safe defaults. */
function normalizeOrder(o) {
    return {
        code: o.orderCode || o.OrderCode || 'N/A',
        itemName: o.itemName || o.ItemName || 'Unknown Item',
        quantity: o.quantity || o.Quantity || 1,
        price: o.price ?? o.Price ?? 0,
        table: o.tableNumber || o.TableNumber || 'N/A',
        status: o.status || o.Status || 'Pending',
        paymentStatus: o.paymentStatus || o.PaymentStatus || 'Unpaid',
        paymentToken: o.paymentToken || o.PaymentToken || '',
        notes: o.notes || o.Notes || '',
        imageUrl: o.imageUrl || o.ImageUrl || '',
        prepMinutes: o.prepMinutes ?? o.PrepMinutes ?? null,
        cookingStartedAt: o.cookingStartedAt || o.CookingStartedAt || ''
    };
}


// ---------- Restaurant name + currency (set by the owner on the admin page) ----------

let appConfig = { restaurantName: 'Restaurant', currencySymbol: '$' };

/** 12.5 -> "$12.50" or "KSh 12.50" (a space is added after a word-like symbol). Returns HTML-safe text. */
function money(amount) {
    const symbol = String(appConfig.currencySymbol || '');
    const gap = symbol.length > 1 && /[A-Za-z]$/.test(symbol) ? ' ' : '';
    const value = Number(amount) || 0;
    return escapeHtml((value < 0 ? '-' : '') + symbol + gap + Math.abs(value).toFixed(2));
}

/** Loads the public config and puts the restaurant name into elements marked data-brand / data-brand-title. */
function loadConfig() {
    return fetch('/api/config')
        .then(res => (res.ok ? res.json() : null))
        .then(config => {
            if (config) appConfig = { ...appConfig, ...config };
        })
        .catch(() => {})
        .then(() => {
            document.querySelectorAll('[data-brand-title]').forEach(el => {
                el.innerText = `${appConfig.restaurantName.toUpperCase()} MENU`;
            });
            if (document.querySelector('[data-brand-title]')) document.title = `${appConfig.restaurantName} - Menu & Ordering`;
        });
}
