// Single-device lock shared by the kitchen and cashier dashboards.
// Needs common.js and auth.js, and the lock overlay markup (#lock-overlay, #handover-code) on the page.

const HEARTBEAT_INTERVAL_MS = 3000;

let deviceSession = null;

/**
 * Registers this browser as the active device for `role` and keeps it alive.
 * options: { role, idPrefix, logoutTitle, logoutHint, onClaimed }
 */
function startDeviceSession(options) {
    deviceSession = {
        ...options,
        deviceId: options.idPrefix + Math.random().toString(36).substring(2, 9)
    };

    verifyDeviceSession();
    setInterval(verifyDeviceSession, HEARTBEAT_INTERVAL_MS);
}

function setLockVisible(visible) {
    byId('lock-overlay').style.display = visible ? 'flex' : 'none';
}

function verifyDeviceSession() {
    if (staffLoggedOut) return; // this device logged out: stop sending heartbeats

    postJson('/api/session/register', { role: deviceSession.role, deviceId: deviceSession.deviceId })
        .then(res => (res.ok ? res.json() : null))   // not logged in: auth.js shows the PIN box
        .then(data => { if (data) setLockVisible(!data.allowed); })
        .catch(() => {});
}

/** "Take Over Session" button: unlock using the code from the other device's logout. */
function claimDashboard() {
    const code = byId('handover-code').value.trim();

    postJson('/api/session/claim', { role: deviceSession.role, deviceId: deviceSession.deviceId, code })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                setLockVisible(false);
                if (deviceSession.onClaimed) deviceSession.onClaimed();
            } else {
                alert(data.message || "Invalid code.");
            }
        })
        .catch(() => alert("Could not reach the server."));
}

/** "Logout Device" button: release the lock and show the handover code. */
function logoutDevice() {
    postJson('/api/session/logout', { role: deviceSession.role })
        .then(res => res.json())
        .then(async data => {
            if (!data.success) return;

            await staffLogout(); // also ends this browser's PIN login

            document.body.innerHTML = `
                <div class="logout-screen">
                    <div class="logout-card">
                        <h2>${escapeHtml(deviceSession.logoutTitle)}</h2>
                        <p>Provide this handover code to the next authorized device operator:</p>
                        <div class="logout-code">${escapeHtml(data.logoutCode)}</div>
                        <p class="logout-hint">${escapeHtml(deviceSession.logoutHint)}</p>
                    </div>
                </div>
            `;
        })
        .catch(() => alert("Could not log out. Please try again."));
}
