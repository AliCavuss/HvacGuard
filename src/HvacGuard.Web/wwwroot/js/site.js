// HvacGuard – shared real-time client (SignalR) used by every authenticated page.
(function () {
  'use strict';

  const handlers = {};
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/telemetry')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const stateEl = document.getElementById('connection-state');
  function setState(css, text) {
    if (!stateEl) return;
    stateEl.classList.remove('online', 'offline');
    if (css) stateEl.classList.add(css);
    stateEl.querySelector('.label').textContent = text;
  }

  function dispatch(event, payload) {
    (handlers[event] || []).forEach(fn => {
      try { fn(payload); } catch (e) { console.error(e); }
    });
  }

  ['reading', 'alarmRaised', 'alarmCleared', 'alarmAcknowledged'].forEach(event =>
    connection.on(event, payload => dispatch(event, payload)));

  connection.onreconnecting(() => setState(null, 'Reconnecting…'));
  connection.onreconnected(() => { setState('online', 'Live'); refreshBadge(); });
  connection.onclose(() => setState('offline', 'Disconnected'));

  async function start() {
    try {
      await connection.start();
      setState('online', 'Live');
    } catch (err) {
      setState('offline', 'Disconnected');
      setTimeout(start, 5000);
    }
  }

  // ---------- Alarm toasts + sidebar badge ----------
  const escapeHtml = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  function toast(alarm) {
    const container = document.getElementById('toast-container');
    if (!container || !window.bootstrap) return;
    const severity = (alarm.severity || '').toLowerCase();
    const el = document.createElement('div');
    el.className = `toast toast-${severity}`;
    el.setAttribute('role', 'alert');
    el.innerHTML = `
      <div class="toast-header bg-transparent text-body">
        <i class="bi ${severity === 'critical' ? 'bi-exclamation-octagon-fill text-danger' : 'bi-exclamation-triangle-fill text-warning'} me-2"></i>
        <strong class="me-auto">${escapeHtml(alarm.vesselName)} · ${escapeHtml(alarm.unitTag)}</strong>
        <small class="text-secondary">${escapeHtml(alarm.severity)}</small>
        <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
      </div>
      <div class="toast-body"><a href="/Units/Details/${alarm.unitId}" class="text-body">${escapeHtml(alarm.message)}</a></div>`;
    container.appendChild(el);
    const t = new bootstrap.Toast(el, { delay: 8000 });
    el.addEventListener('hidden.bs.toast', () => el.remove());
    t.show();
  }

  let badgeTimer;
  async function refreshBadge() {
    try {
      const res = await fetch('/Dashboard/Kpis', { headers: { 'Accept': 'application/json' } });
      if (!res.ok) return;
      const data = await res.json();
      const badge = document.getElementById('nav-alarm-badge');
      if (badge) {
        const n = data.kpis.unacknowledgedAlarmCount;
        badge.textContent = n;
        badge.classList.toggle('d-none', n === 0);
      }
      dispatch('kpis', data);
    } catch { /* ignore transient errors */ }
  }
  const scheduleBadge = () => { clearTimeout(badgeTimer); badgeTimer = setTimeout(refreshBadge, 400); };

  window.hvac = {
    connection,
    on(event, fn) { (handlers[event] = handlers[event] || []).push(fn); },
    refreshKpis: scheduleBadge,
    escapeHtml
  };

  window.hvac.on('alarmRaised', toast);
  ['alarmRaised', 'alarmCleared', 'alarmAcknowledged'].forEach(e => window.hvac.on(e, scheduleBadge));

  document.addEventListener('DOMContentLoaded', () => { start(); refreshBadge(); });
})();
