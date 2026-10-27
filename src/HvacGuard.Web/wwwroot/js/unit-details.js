// Live charts and values for a single air handling unit.
(function () {
  'use strict';
  const root = document.getElementById('unit-live');
  if (!root || !window.Chart) return;

  const unitId = Number(root.dataset.unitId);
  const thresholds = JSON.parse(root.dataset.thresholds || '[]');
  const MAX_POINTS = 600;

  Chart.defaults.color = '#8a9bb8';
  Chart.defaults.borderColor = 'rgba(138,155,184,0.12)';
  Chart.defaults.font.family = 'Inter, system-ui, sans-serif';

  const label = ts => new Date(ts.endsWith && !ts.endsWith('Z') ? ts + 'Z' : ts).toISOString().substring(11, 19);
  const line = (labelText, color, axis = 'y', dashed = false) => ({
    label: labelText, data: [], borderColor: color, backgroundColor: color, yAxisID: axis,
    borderWidth: dashed ? 1 : 2, pointRadius: 0, tension: 0.25, borderDash: dashed ? [6, 4] : [],
  });

  // Threshold lines for the supply air temperature (from the enabled alarm rules).
  const supplyLimits = thresholds.filter(t => t.parameter === 'SupplyAirTemperature');
  const tempChart = new Chart(document.getElementById('chart-temperature'), {
    type: 'line',
    data: {
      labels: [],
      datasets: [
        line('Supply air', '#2dd4bf'),
        line('Return air', '#38bdf8'),
        ...supplyLimits.map(t => ({ ...line(`Limit ${t.condition === 'Above' ? '>' : '<'} ${t.threshold} (${t.severity})`,
          t.severity === 'Critical' ? '#ef4444' : '#f59e0b', 'y', true), limit: t.threshold }))
      ]
    },
    options: { animation: false, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
      scales: { x: { ticks: { maxTicksLimit: 8 } }, y: { title: { display: true, text: '°C' } } },
      plugins: { legend: { labels: { boxWidth: 12 } } } }
  });

  const airChart = new Chart(document.getElementById('chart-airflow'), {
    type: 'line',
    data: { labels: [], datasets: [line('Fan speed (%)', '#a78bfa', 'y'), line('Filter ΔP (Pa)', '#f59e0b', 'y1')] },
    options: { animation: false, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
      scales: {
        x: { ticks: { maxTicksLimit: 8 } },
        y: { min: 0, max: 100, title: { display: true, text: '%' } },
        y1: { position: 'right', min: 0, grid: { drawOnChartArea: false }, title: { display: true, text: 'Pa' } }
      },
      plugins: { legend: { labels: { boxWidth: 12 } } } }
  });

  function push(r) {
    const l = label(r.timestamp);
    tempChart.data.labels.push(l);
    tempChart.data.datasets[0].data.push(r.supplyAirTemperature);
    tempChart.data.datasets[1].data.push(r.returnAirTemperature);
    tempChart.data.datasets.slice(2).forEach(ds => ds.data.push(ds.limit));

    airChart.data.labels.push(l);
    airChart.data.datasets[0].data.push(r.fanSpeed);
    airChart.data.datasets[1].data.push(r.filterPressureDrop);

    [tempChart, airChart].forEach(c => {
      while (c.data.labels.length > MAX_POINTS) {
        c.data.labels.shift();
        c.data.datasets.forEach(ds => ds.data.shift());
      }
    });
  }

  function updateTiles(r) {
    const set = (field, text) => {
      const el = root.querySelector(`[data-field="${field}"]`);
      if (el && el.textContent !== text) { el.textContent = text; el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash'); }
    };
    set('supplyAirTemperature', `${r.supplyAirTemperature.toFixed(1)} °C`);
    set('returnAirTemperature', `${r.returnAirTemperature.toFixed(1)} °C`);
    set('relativeHumidity', `${r.relativeHumidity.toFixed(1)} %RH`);
    set('fanSpeed', `${r.fanSpeed.toFixed(1)} %`);
    set('filterPressureDrop', `${r.filterPressureDrop.toFixed(0)} Pa`);
  }

  fetch(`/api/units/${unitId}/readings?minutes=30`, { headers: { 'Accept': 'application/json' } })
    .then(res => res.ok ? res.json() : [])
    .then(rows => {
      rows.forEach(push);
      tempChart.update();
      airChart.update();
    })
    .catch(console.error);

  window.hvac.on('reading', r => {
    if (r.unitId !== unitId) return;
    push(r);
    updateTiles(r);
    tempChart.update('none');
    airChart.update('none');
  });

  // Alarm list on this page is server-rendered; refresh it when this unit's alarms change.
  let reloadTimer;
  const reloadIfMine = a => {
    if (a.unitId !== unitId) return;
    clearTimeout(reloadTimer);
    reloadTimer = setTimeout(() => location.reload(), 2000);
  };
  ['alarmRaised', 'alarmCleared', 'alarmAcknowledged'].forEach(e => window.hvac.on(e, reloadIfMine));
})();
