// Live updates for the fleet dashboard and vessel pages (unit cards, KPIs, alarm feed).
(function () {
  'use strict';
  const fmt = (v, digits) => (v === null || v === undefined) ? '—' : Number(v).toFixed(digits);
  const units = { supplyAirTemperature: ['°C', 1], returnAirTemperature: ['°C', 1], relativeHumidity: ['%RH', 1], fanSpeed: ['%', 1], filterPressureDrop: ['Pa', 0] };
  const stateClasses = ['unit-normal', 'unit-warning', 'unit-critical', 'unit-offline'];

  function setField(root, field, text) {
    const el = root.querySelector(`[data-field="${field}"]`);
    if (!el || el.textContent === text) return;
    el.textContent = text;
    el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash');
  }

  function applySeverity(card, severity) {
    if (card.classList.contains('unit-inactive')) return;
    stateClasses.forEach(c => card.classList.remove(c));
    const css = severity === 'Critical' ? 'unit-critical' : severity === 'Warning' ? 'unit-warning' : 'unit-normal';
    card.classList.add(css);
    const pill = card.querySelector('[data-field="state"]');
    if (pill) pill.textContent = severity || 'Normal';
  }

  window.hvac.on('reading', r => {
    document.querySelectorAll(`.unit-card[data-unit-id="${r.unitId}"]`).forEach(card => {
      Object.entries(units).forEach(([key, [unit, digits]]) => setField(card, key, `${fmt(r[key], digits)} ${unit}`));
      card.dataset.lastSeen = r.timestamp;
      setField(card, 'lastSeen', 'just now');
      if (card.classList.contains('unit-offline')) applySeverity(card, null);
    });
  });

  window.hvac.on('alarmRaised', a => {
    const feed = document.getElementById('alarm-feed');
    if (!feed) return;
    const li = document.createElement('li');
    li.className = `new severity-${a.severity.toLowerCase()}`;
    li.dataset.alarmId = a.id;
    li.innerHTML = `
      <div class="d-flex justify-content-between">
        <strong>${window.hvac.escapeHtml(a.vesselName)} · ${window.hvac.escapeHtml(a.unitTag)}</strong>
        <span class="badge text-bg-danger blink" data-field="alarm-state">Active – unacknowledged</span>
      </div>
      <div class="small">${window.hvac.escapeHtml(a.message)}</div>
      <div class="small text-secondary">${new Date(a.raisedAt).toISOString().substring(5, 19).replace('T', ' ')} UTC</div>`;
    feed.prepend(li);
    while (feed.children.length > 8) feed.lastElementChild.remove();
  });

  const updateFeedState = (id, text, css) => {
    const badge = document.querySelector(`#alarm-feed li[data-alarm-id="${id}"] [data-field="alarm-state"]`);
    if (badge) { badge.className = `badge ${css}`; badge.textContent = text; }
  };
  window.hvac.on('alarmCleared', a => updateFeedState(a.id, 'Cleared', 'text-bg-info'));
  window.hvac.on('alarmAcknowledged', a => updateFeedState(a.id, 'Acknowledged', 'text-bg-warning'));

  // KPI counters + per-unit severity are refreshed from the server after every alarm event.
  window.hvac.on('kpis', data => {
    Object.entries(data.kpis).forEach(([key, value]) => {
      document.querySelectorAll(`[data-kpi="${key}"]`).forEach(el => {
        if (el.textContent !== String(value)) { el.textContent = value; el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash'); }
      });
    });
    data.units.forEach(u => document.querySelectorAll(`.unit-card[data-unit-id="${u.id}"]`).forEach(card => {
      applySeverity(card, u.severity);
      setField(card, 'openAlarmCount', String(u.openAlarmCount));
    }));
  });

  // Mark units offline when no reading arrived for 60 s; tick the UTC clock.
  setInterval(() => {
    const now = Date.now();
    document.querySelectorAll('.unit-card[data-last-seen]').forEach(card => {
      const last = Date.parse(card.dataset.lastSeen.endsWith('Z') ? card.dataset.lastSeen : card.dataset.lastSeen + 'Z');
      if (!isNaN(last) && now - last > 60000 && !card.classList.contains('unit-inactive')) {
        stateClasses.forEach(c => card.classList.remove(c));
        card.classList.add('unit-offline');
        const pill = card.querySelector('[data-field="state"]');
        if (pill) pill.textContent = 'Offline';
      }
    });
    const clock = document.getElementById('utc-clock');
    if (clock) clock.textContent = new Date().toISOString().substring(11, 19);
  }, 1000);
})();
