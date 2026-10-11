// Owner admin page: menu editor, sales report, settings. Needs common.js and auth.js.
// Everything shown comes from the server and is escaped; buttons carry ids in data-* attributes.

let menuItems = [];
let editingId = null; // null = adding a new item

// ---------- Small helpers ----------

/** Fetch JSON from an owner API. Returns { ok, data }. A 401 pops up the PIN box (see auth.js). */
async function api(method, url, body) {
    const options = { method, headers: {} };
    if (body !== undefined) {
        options.headers['Content-Type'] = 'application/json';
        options.body = JSON.stringify(body);
    }
    const res = await apiFetch(url, options);
    const data = await res.json().catch(() => ({}));
    return { ok: res.ok, data };
}

function setText(id, text, isOk = false) {
    const el = byId(id);
    el.innerText = text || '';
    el.classList.toggle('form-ok', isOk);
}

function localDate(daysAgo = 0) {
    const d = new Date();
    d.setDate(d.getDate() - daysAgo);
    return d.toLocaleDateString('sv-SE'); // yyyy-MM-dd in this device's time zone
}

// ---------- Tabs ----------

function showTab(name) {
    document.querySelectorAll('.admin-tab').forEach(b => b.classList.toggle('active', b.dataset.tab === name));
    document.querySelectorAll('.admin-panel').forEach(p => { p.hidden = p.id !== `tab-${name}`; });

    if (name === 'menu') loadMenu();
    if (name === 'sales') runReport();
    if (name === 'settings') loadSettings();
    if (name === 'tables') initTablesTab();
}

// ---------- Menu ----------

async function loadMenu() {
    const { ok, data } = await api('GET', '/api/admin/menu');
    if (!ok) return;
    menuItems = data;
    renderMenu();
}

function renderMenu() {
    const list = byId('menu-list');

    if (menuItems.length === 0) {
        list.innerHTML = `<div class="empty-note">Your menu is empty. Click "+ Add item" to create your first dish.</div>`;
        return;
    }

    list.innerHTML = menuItems.map(item => `
        <div class="menu-row ${item.isAvailable ? '' : 'off'}">
            ${item.imageUrl
                ? `<img class="menu-thumb" src="${escapeHtml(item.imageUrl)}" alt="">`
                : `<div class="menu-thumb"></div>`}
            <div>
                <div class="menu-name">${escapeHtml(item.name)}</div>
                <div class="menu-meta">${escapeHtml(item.category)} · ${escapeHtml(item.dietary)}${item.isAvailable ? '' : ' · HIDDEN'}</div>
            </div>
            <div class="menu-price">${money(item.price)}</div>
            <div class="row-actions">
                <button class="btn-small" data-act="toggle" data-id="${Number(item.id)}">${item.isAvailable ? 'Switch off' : 'Switch on'}</button>
                <button class="btn-small" data-act="edit" data-id="${Number(item.id)}">Edit</button>
                <button class="btn-danger-outline btn-small" data-act="delete" data-id="${Number(item.id)}">Delete</button>
            </div>
        </div>
    `).join('');
}

function itemToRequest(item, overrides = {}) {
    return {
        name: item.name,
        price: item.price,
        description: item.description,
        category: item.category,
        dietary: item.dietary,
        imageUrl: item.imageUrl,
        isAvailable: item.isAvailable,
        ...overrides
    };
}

async function handleMenuClick(event) {
    const btn = event.target.closest('[data-act]');
    if (!btn) return;

    const id = Number(btn.dataset.id);
    const item = menuItems.find(m => m.id === id);
    if (!item) return;

    if (btn.dataset.act === 'edit') {
        openItemDialog(item);
    } else if (btn.dataset.act === 'toggle') {
        const { ok, data } = await api('PUT', `/api/admin/menu/${id}`, itemToRequest(item, { isAvailable: !item.isAvailable }));
        if (!ok) alert(data.message || 'Could not update the item.');
        loadMenu();
    } else if (btn.dataset.act === 'delete') {
        if (!confirm(`Delete "${item.name}" from the menu?\n\nPast orders and sales reports are not affected.`)) return;
        const { ok, data } = await api('DELETE', `/api/admin/menu/${id}`);
        if (!ok) alert(data.message || 'Could not delete the item.');
        loadMenu();
    }
}

async function clearMenu() {
    if (menuItems.length === 0) return;
    if (!confirm(`Remove ALL ${menuItems.length} items from the menu?\n\nThis cannot be undone. Past orders and sales reports are not affected.`)) return;
    const { ok, data } = await api('DELETE', '/api/admin/menu');
    if (!ok) alert(data.message || 'Could not clear the menu.');
    loadMenu();
}

// ----- add / edit dialog -----

function showPreview(url) {
    const img = byId('item-preview');
    if (url) { img.src = url; img.hidden = false; } else { img.removeAttribute('src'); img.hidden = true; }
}

function openItemDialog(item) {
    editingId = item ? item.id : null;
    byId('item-dialog-title').innerText = item ? 'Edit item' : 'Add item';
    byId('item-name').value = item ? item.name : '';
    byId('item-price').value = item ? item.price : '';
    byId('item-category').value = item ? item.category : 'Food';
    byId('item-dietary').value = item ? item.dietary : 'Non-Vegan';
    byId('item-description').value = item ? item.description : '';
    byId('item-image-url').value = item ? item.imageUrl : '';
    byId('item-available').checked = item ? item.isAvailable : true;
    byId('item-photo').value = '';
    setText('item-error', '');
    showPreview(item ? item.imageUrl : '');
    byId('item-dialog').hidden = false;
    byId('item-name').focus();
}

function closeItemDialog() {
    byId('item-dialog').hidden = true;
}

/** Sends the chosen photo file to the server and puts the returned address into the form. */
async function uploadPhoto(file) {
    setText('item-error', 'Uploading photo...', true);

    const res = await apiFetch('/api/admin/menu/image', {
        method: 'POST',
        headers: { 'Content-Type': file.type || 'application/octet-stream' },
        body: file
    });
    const data = await res.json().catch(() => ({}));

    if (!res.ok) {
        byId('item-photo').value = '';
        setText('item-error', data.message || 'Photo upload failed.');
        return;
    }

    byId('item-image-url').value = data.url;
    showPreview(data.url);
    setText('item-error', '');
}

async function saveItem() {
    const request = {
        name: byId('item-name').value.trim(),
        price: parseFloat(byId('item-price').value),
        description: byId('item-description').value.trim(),
        category: byId('item-category').value,
        dietary: byId('item-dietary').value,
        imageUrl: byId('item-image-url').value.trim(),
        isAvailable: byId('item-available').checked
    };

    if (!request.name) return setText('item-error', 'Please enter a name.');
    if (!(request.price > 0)) return setText('item-error', 'Please enter a price above 0.');

    const { ok, data } = editingId === null
        ? await api('POST', '/api/admin/menu', request)
        : await api('PUT', `/api/admin/menu/${editingId}`, request);

    if (!ok) return setText('item-error', data.message || 'Could not save the item.');

    closeItemDialog();
    loadMenu();
}

// ---------- Sales report ----------

function tzOffsetMinutes() {
    return -new Date().getTimezoneOffset(); // Nairobi = 180
}

function reportQuery() {
    const from = encodeURIComponent(byId('report-from').value);
    const to = encodeURIComponent(byId('report-to').value);
    return `from=${from}&to=${to}&tzOffset=${tzOffsetMinutes()}`;
}

async function runReport() {
    setText('report-error', '');
    byId('export-csv-link').href = `/api/admin/report.csv?${reportQuery()}`;

    const { ok, data } = await api('GET', `/api/admin/report?${reportQuery()}`);
    if (!ok) {
        setText('report-error', data.message || 'Could not load the report.');
        return;
    }

    byId('report-cards').innerHTML = [
        ['Sales', money(data.revenue)],
        ['Bills settled', escapeHtml(data.bills)],
        ['Items sold', escapeHtml(data.items)],
        ['Average bill', money(data.averageBill)],
        ['Discounts given', money(data.discounts)]
    ].map(([label, value]) => `
        <div class="stat-card"><div class="stat-label">${label}</div><div class="stat-value">${value}</div></div>
    `).join('');

    byId('report-daily').innerHTML = data.daily.length === 0
        ? `<div class="empty-note">No settled bills in this period.</div>`
        : `<table class="report"><thead><tr><th>Date</th><th class="num">Bills</th><th class="num">Items</th><th class="num">Sales</th></tr></thead><tbody>
            ${data.daily.map(d => `<tr><td>${escapeHtml(d.date)}</td><td class="num">${escapeHtml(d.bills)}</td><td class="num">${escapeHtml(d.items)}</td><td class="num">${money(d.revenue)}</td></tr>`).join('')}
           </tbody></table>`;

    byId('report-top').innerHTML = data.top.length === 0
        ? `<div class="empty-note">Nothing sold yet.</div>`
        : `<table class="report"><thead><tr><th>Item</th><th class="num">Sold</th><th class="num">Sales</th></tr></thead><tbody>
            ${data.top.map(t => `<tr><td>${escapeHtml(t.name)}</td><td class="num">${escapeHtml(t.quantity)}</td><td class="num">${money(t.revenue)}</td></tr>`).join('')}
           </tbody></table>`;

    if (data.truncated) setText('report-error', 'This range has a very large number of orders; only the first part is shown. Pick a shorter range.');
}

function setRange(daysBack) {
    byId('report-from').value = localDate(Number(daysBack));
    byId('report-to').value = localDate(0);
    runReport();
}

// ---------- Settings ----------

async function loadSettings() {
    setText('settings-msg', '');
    const { ok, data } = await api('GET', '/api/admin/settings');
    if (!ok) return;
    byId('set-name').value = data.restaurantName;
    byId('set-currency').value = data.currencySymbol;
    byId('set-ip').checked = data.storeIpAddresses;
    byId('set-require-key').checked = data.requireTableKey;
    byId('set-max-discount').value = data.maxDiscountPercent;
    byId('set-tax').value = data.taxPercent;
    byId('set-taxid').value = data.taxId || '';
    byId('set-footer').value = data.receiptFooter || '';
}

async function saveSettings() {
    const wasChecked = byId('set-ip').checked;
    const { ok, data } = await api('PUT', '/api/admin/settings', {
        restaurantName: byId('set-name').value,
        currencySymbol: byId('set-currency').value,
        storeIpAddresses: wasChecked,
        requireTableKey: byId('set-require-key').checked,
        maxCashierDiscountPercent: Number(byId('set-max-discount').value) || 0,
        taxPercent: Number(byId('set-tax').value) || 0,
        taxId: byId('set-taxid').value,
        receiptFooter: byId('set-footer').value
    });

    if (!ok) return setText('settings-msg', data.message || 'Could not save settings.');

    await loadConfig(); // prices on this page use the new symbol straight away
    setText('settings-msg', 'Saved.', true);
}

// ---------- Table QR codes ----------

function initTablesTab() {
    const baseInput = byId('qr-base');
    if (!baseInput.value) {
        const saved = localStorage.getItem('qr_base');
        const isLocal = ['localhost', '127.0.0.1', '[::1]'].includes(location.hostname);
        baseInput.value = saved || (isLocal ? '' : location.origin);
        if (isLocal && !saved) setText('qr-error', 'You opened this page as "localhost". Type the address phones use, e.g. http://192.168.1.20:5000');
    }
}

async function showQrCodes() {
    const base = byId('qr-base').value.trim().replace(/\/+$/, '');
    const count = Math.min(50, Math.max(1, parseInt(byId('qr-count').value, 10) || 1));

    if (!/^https?:\/\/[^\s/]+/i.test(base) || /^https?:\/\/(localhost|127\.|\[::1\])/i.test(base)) {
        return setText('qr-error', 'Type the address phones use to reach this computer, e.g. http://192.168.1.20:5000 (not localhost).');
    }
    setText('qr-error', '');
    localStorage.setItem('qr_base', base);

    const { ok, data } = await api('GET', `/api/admin/table-keys?count=${count}`);
    if (!ok) return setText('qr-error', data.message || 'Could not load the table codes.');

    byId('qr-grid').innerHTML = data.map(row => {
        const url = `${base}/?t=${encodeURIComponent(row.table)}&k=${encodeURIComponent(row.key)}`;
        return `
            <div class="qr-card">
                <div class="qr-name">${escapeHtml(appConfig.restaurantName)}</div>
                <div class="qr-img">${window.makeQr(url)}</div>
                <div class="qr-table">Table ${escapeHtml(row.table)}</div>
                <div class="qr-hint">Scan to see the menu and order</div>
            </div>`;
    }).join('');
}

// ---------- Start-up ----------

document.addEventListener('DOMContentLoaded', () => {
    initStaffAuth('owner', () => showTab(document.querySelector('.admin-tab.active').dataset.tab));

    document.querySelectorAll('.admin-tab').forEach(b => b.addEventListener('click', () => showTab(b.dataset.tab)));
    byId('menu-list').addEventListener('click', handleMenuClick);
    byId('add-item-btn').addEventListener('click', () => openItemDialog(null));
    byId('clear-menu-btn').addEventListener('click', clearMenu);

    byId('item-cancel').addEventListener('click', closeItemDialog);
    byId('item-save').addEventListener('click', saveItem);
    byId('item-photo').addEventListener('change', e => { if (e.target.files[0]) uploadPhoto(e.target.files[0]); });
    byId('item-image-url').addEventListener('input', e => showPreview(e.target.value.trim()));

    byId('run-report-btn').addEventListener('click', runReport);
    document.querySelectorAll('[data-range]').forEach(b => b.addEventListener('click', () => setRange(b.dataset.range)));
    byId('report-from').addEventListener('change', () => { byId('export-csv-link').href = `/api/admin/report.csv?${reportQuery()}`; });
    byId('report-to').addEventListener('change', () => { byId('export-csv-link').href = `/api/admin/report.csv?${reportQuery()}`; });

    byId('save-settings-btn').addEventListener('click', saveSettings);
    byId('qr-show-btn').addEventListener('click', showQrCodes);
    byId('qr-print-btn').addEventListener('click', () => {
        if (!byId('qr-grid').children.length) return setText('qr-error', 'Click "Show QR codes" first.');
        window.print();
    });

    byId('admin-logout').addEventListener('click', async () => {
        await staffLogout();
        location.reload();
    });

    byId('report-from').value = localDate(0);
    byId('report-to').value = localDate(0);

    loadConfig().then(() => showTab('menu')); // a 401 here shows the PIN box
});
