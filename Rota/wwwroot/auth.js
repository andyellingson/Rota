window.authPost = async function (url, data) {
  const resp = await fetch(url, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  // Read text first to avoid "body stream already read" errors, then try to parse JSON from it.
  const text = await resp.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch (e) {
    // ignore json parse errors
  }
  return { ok: resp.ok, status: resp.status, body: body, text: text };
};

// Helper for authenticated GET requests
window.authGet = async function (url) {
  const resp = await fetch(url, {
    method: 'GET',
    credentials: 'same-origin'
  });
  const text = await resp.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch (e) {
    // ignore json parse errors
  }
  return { ok: resp.ok, status: resp.status, body: body, text: text, ...body };
};

// Helper for authenticated DELETE requests
window.authDelete = async function (url) {
  const resp = await fetch(url, {
    method: 'DELETE',
    credentials: 'same-origin'
  });
  const text = await resp.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch (e) {
    // ignore json parse errors
  }
  return { ok: resp.ok, status: resp.status, body: body, text: text };
};

// Helper for authenticated PUT requests
window.authPut = async function (url, data) {
  const resp = await fetch(url, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  const text = await resp.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch (e) {
    // ignore json parse errors
  }
  return { ok: resp.ok, status: resp.status, body: body, text: text };
};

// Applies the given theme by setting the data-theme attribute on <html>.
// Call with "light" or "dark"; defaults to "light" for any unrecognised value.
window.setTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
};

// Opens a new browser window containing only the schedule preview and triggers
// the browser's native print dialog. This approach bypasses Blazor CSS isolation
// (which mangles @media print selectors in scoped .razor.css files) by writing
// fresh, un-scoped CSS into the print window alongside the copied preview HTML.
window.printSchedule = function (elementId) {
    const el = document.getElementById(elementId);
    if (!el) return;

    // Copy the preview HTML. Blazor adds b-xxxx scope attributes to elements but
    // we target plain class names in the print window's CSS so they still match.
    const content = el.innerHTML;

    const printWindow = window.open('', '_blank', 'width=960,height=720');
    if (!printWindow) return;

    printWindow.document.write(`<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Print Schedule</title>
  <style>
    /* Reset */
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    html, body { height: 100%; }
    body { font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background: #fff; color: #111; padding: 0; margin: 0; }

    /* Make the preview container match the modal preview: scaled to 90% and no surrounding margins */
    .print-preview-container {
      background: #fff;
      color: #111;
      border-radius: 6px;
      padding: 1.25rem 1.5rem;
      min-height: 400px;
      transform: scale(0.9);
      transform-origin: top center;
      margin: 0 auto;
      width: 100%;
    }

    /* ── Document header ── */
    .print-doc-header  { display: flex; align-items: baseline; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.6rem; padding-bottom: 0.4rem; border-bottom: 1px solid #dee2e6; }
    .print-doc-title   { font-size: 1rem; font-weight: 700; white-space: nowrap; }
    .print-doc-subtitle{ font-size: 0.82rem; color: #444; white-space: nowrap; }
    .print-doc-meta    { font-size: 0.72rem; color: #999; margin-left: auto; white-space: nowrap; }

    /* ── Month grid ── */
    .print-month-grid  { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 3px; }
    .print-month-header{ font-weight: 600; text-align: center; padding: 0.3rem 0; font-size: 0.75rem; background: #f0f0f0; color: #444; border-radius: 3px; }
    .print-day-cell    { min-height: 90px; background: #fff; border: 1px solid #e0e0e0; border-radius: 3px; padding: 0.25rem; font-size: 0.72rem; display: flex; flex-direction: column; gap: 3px; }
    .print-day-other   { background: #fafafa; color: #aaa; }
    .print-day-today   { border-color: #1890ff; background: #e8f4ff; }
    .print-day-number  { font-weight: 600; font-size: 0.8rem; text-align: right; color: #555; margin-bottom: 2px; }
    .print-day-other .print-day-number { color: #bbb; }

    /* ── Week grid ── */
    .print-week-grid   { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 4px; }
    .print-week-col    { border: 1px solid #e0e0e0; border-radius: 4px; padding: 0.4rem 0.35rem; background: #fff; min-height: 180px; display: flex; flex-direction: column; gap: 4px; }
    .print-week-col.print-day-today { border-color: #1890ff; background: #e8f4ff; }
    .print-week-day-header { text-align: center; padding-bottom: 0.4rem; border-bottom: 1px solid #e9ecef; margin-bottom: 0.3rem; font-size: 0.8rem; font-weight: 700; color: #333; }
    .print-week-day-date   { font-size: 0.7rem; font-weight: 400; color: #666; }
    .print-week-empty      { color: #bbb; font-size: 0.75rem; text-align: center; margin-top: 0.5rem; }

    /* ── Event chips ── */
    .print-event         { border-left: 3px solid #1890ff; border-radius: 2px; padding: 2px 5px; display: flex; flex-direction: column; gap: 1px; background: #f5f9ff; }
    .print-event-absence { background: #fff8f0; border-left-color: #fa8c16; }
    .print-event-reminder{ background: #fffde7; border-left-color: #f0c000; }
    .print-event-header  { display:flex; align-items:center; justify-content:space-between; gap:0.5rem; }
    .print-event-worker  { font-weight:700; font-size:0.78rem; color:#1890ff; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; min-width:0; }
    .print-event-time    { font-size:0.72rem; color:#555; white-space:nowrap; }
    .print-event-person  { font-size:0.78rem; color:#111; font-weight:700; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }

    /* ── Multi-period sections ── */
    .print-period-section { margin-bottom: 1rem; }
    .print-period-header  { font-size: 1rem; font-weight: 700; color: #222; margin-bottom: 0.5rem; padding-bottom: 0.25rem; border-bottom: 1px solid #dee2e6; }
    .print-period-section--pair-top { border-bottom: 1px solid #e2e8f0; padding-bottom: 0.75rem; margin-bottom: 0.75rem; }

    @media print {
      /* Remove margins when printing so content uses full page width */
      html, body { margin: 0; padding: 0; }
      .print-preview-container { transform: none; width: 100%; padding: 0.5cm; }
      .print-day-cell, .print-week-col { break-inside: avoid; }
      .print-period-section--break { page-break-after: always; break-after: page; }
    }
  </style>
</head>
<body>${content}</body>
</html>`);

    printWindow.document.close();
    printWindow.focus();
    // Small delay so the browser finishes rendering before showing the print dialog.
    setTimeout(function () {
        printWindow.print();
        printWindow.close();
    }, 300);
};
