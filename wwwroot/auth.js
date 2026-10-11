// Staff PIN login for the kitchen and cashier pages. Needs common.js. Load it before session.js.
//
// The page itself is public, but every staff API call is rejected by the server (HTTP 401) until
// someone has logged in, so this file just shows a PIN box whenever that happens.

let staffRole = null;
let staffLoggedOut = false;
let staffOnLoggedIn = null;

/** Call once on page load. onLoggedIn runs after every successful login (reload your data there). */
function initStaffAuth(role, onLoggedIn) {
    staffRole = role;
    staffOnLoggedIn = onLoggedIn;
}

/** Called by apiFetch() whenever the server says "login required". */
function onStaffUnauthorized() {
    if (!staffRole || staffLoggedOut) return;
    showStaffLogin();
}

function showStaffLogin(message) {
    let overlay = byId('staff-login-overlay');

    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'staff-login-overlay';
        overlay.className = 'lock-overlay';
        overlay.style.zIndex = '100000';
        overlay.innerHTML = `
            <div class="lock-card">
                <h2 class="lock-title">Staff Login</h2>
                <p class="lock-msg">Enter the ${escapeHtml(staffRole)} PIN to continue.</p>
                <p id="staff-login-error" class="lock-msg" style="color:#ff8a80;min-height:1.2em;"></p>
                <input type="password" id="staff-pin" class="lock-input" placeholder="PIN"
                       inputmode="numeric" autocomplete="current-password" maxlength="64">
                <button id="staff-login-btn" class="lock-btn" type="button">Log in</button>
            </div>`;
        document.body.appendChild(overlay);

        byId('staff-login-btn').addEventListener('click', submitStaffLogin);
        byId('staff-pin').addEventListener('keydown', e => { if (e.key === 'Enter') submitStaffLogin(); });
    }

    byId('staff-login-error').innerText = message || '';
    overlay.style.display = 'flex';
    byId('staff-pin').focus();
}

function submitStaffLogin() {
    const pinInput = byId('staff-pin');
    const pin = pinInput.value;
    if (!pin) return;

    postJson('/api/staff/login', { role: staffRole, pin })
        .then(res => res.json().catch(() => ({})).then(data => ({ ok: res.ok, data })))
        .then(({ ok, data }) => {
            if (!ok) {
                pinInput.value = '';
                byId('staff-login-error').innerText = data.message || 'Login failed.';
                pinInput.focus();
                return;
            }

            pinInput.value = '';
            byId('staff-login-overlay').style.display = 'none';
            if (staffOnLoggedIn) staffOnLoggedIn();
        })
        .catch(() => { byId('staff-login-error').innerText = 'Could not reach the server.'; });
}

/** Ends this browser's login (the dashboards call this from "Logout Device"). */
function staffLogout() {
    staffLoggedOut = true; // stops the polling loops from popping the login box back up
    return postJson('/api/staff/logout', { role: staffRole }).catch(() => {});
}
