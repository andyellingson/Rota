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
