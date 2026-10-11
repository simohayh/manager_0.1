// Shared by the kitchen and cashier pages: "connection lost" banner, new-order sound and flashing title.
// Load after common.js.

let audioCtx = null;
let soundOn = localStorage.getItem('staff_sound') !== 'off';
const BASE_TITLE = document.title;
let titleFlashTimer = null;

// ---------- Connection banner ----------

/** Shows a red bar while the dashboard cannot reach the server, so staff don't trust a frozen screen. */
function setConnectionLost(lost) {
    let bar = byId('conn-banner');
    if (!bar) {
        bar = document.createElement('div');
        bar.id = 'conn-banner';
        bar.className = 'conn-banner';
        bar.textContent = '⚠️ Connection lost. This screen is NOT up to date. Check the Wi-Fi / the computer running the app.';
        bar.hidden = true;
        document.body.prepend(bar);
    }
    bar.hidden = !lost;
}

// ---------- Sound ----------

function unlockAudio() {
    try {
        if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        if (audioCtx.state === 'suspended') audioCtx.resume();
    } catch (e) { /* no audio support */ }
    updateSoundButton();
}

function soundReady() {
    return !!audioCtx && audioCtx.state === 'running';
}

function updateSoundButton() {
    const btn = byId('sound-btn');
    if (!btn) return;
    if (!soundReady()) btn.textContent = '🔕 Tap to enable sound';
    else btn.textContent = soundOn ? '🔔 Sound on' : '🔕 Sound off';
}

function onSoundButton() {
    if (!soundReady()) { unlockAudio(); return; }
    soundOn = !soundOn;
    localStorage.setItem('staff_sound', soundOn ? 'on' : 'off');
    updateSoundButton();
    if (soundOn) beep(1);
}

/** A short repeating beep (works without any sound file). */
function beep(times = 3) {
    if (!soundOn || !soundReady()) return;
    for (let i = 0; i < times; i++) {
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'sine';
        osc.frequency.value = i % 2 === 0 ? 880 : 1175;
        gain.gain.value = 0.25;
        osc.connect(gain);
        gain.connect(audioCtx.destination);
        const start = audioCtx.currentTime + i * 0.35;
        osc.start(start);
        osc.stop(start + 0.25);
    }
}

// ---------- New-order alert ----------

function alertNewOrders(count, label) {
    beep(3);

    let bar = byId('new-order-banner');
    if (!bar) {
        bar = document.createElement('div');
        bar.id = 'new-order-banner';
        bar.className = 'new-order-banner';
        document.body.prepend(bar);
    }
    bar.textContent = `🔔 ${count} ${label}`;
    bar.hidden = false;
    setTimeout(() => { bar.hidden = true; }, 10000);

    // Flash the browser tab title too (helps when the tablet screen is dimmed).
    clearInterval(titleFlashTimer);
    let on = false;
    let flashes = 0;
    titleFlashTimer = setInterval(() => {
        document.title = on ? BASE_TITLE : `🔔 ${count} ${label}`;
        on = !on;
        if (++flashes > 20) { clearInterval(titleFlashTimer); document.title = BASE_TITLE; }
    }, 700);
}

document.addEventListener('click', unlockAudio);
document.addEventListener('DOMContentLoaded', updateSoundButton);
