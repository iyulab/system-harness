const { spawn } = require('child_process');
const proc = spawn('dotnet', ['run', '--project', 'src/SystemHarness.Mcp', '--no-build'], {
  stdio: ['pipe', 'pipe', 'pipe'],
  cwd: process.cwd()
});

let buf = '';
proc.stdout.on('data', d => { buf += d.toString(); });
proc.stderr.on('data', () => {});

let idCounter = 10;
function send(obj) { proc.stdin.write(JSON.stringify(obj) + '\n'); }
function call(name, args) {
  const id = idCounter++;
  send({ jsonrpc: '2.0', id, method: 'tools/call', params: { name, arguments: args } });
  return id;
}

// Initialize
send({
  jsonrpc: '2.0', id: 1, method: 'initialize',
  params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'test', version: '1.0' } }
});

const tests = [];

// === Batch 1: Non-file-dependent tests (sent at 500ms) ===
setTimeout(() => {
  send({ jsonrpc: '2.0', method: 'notifications/initialized' });

  // === Scenario 1: Discovery Flow ===
  tests.push({ id: call('help', {}), desc: 'help() — category list' });
  tests.push({ id: call('help', { topic: 'file' }), desc: 'help(file) — file category' });
  tests.push({ id: call('help', { topic: 'file.read' }), desc: 'help(file.read) — param details' });
  tests.push({ id: call('help', { topic: 'safety' }), desc: 'help(safety) — safety category' });
  tests.push({ id: call('help', { topic: 'vision' }), desc: 'help(vision) — vision category' });

  // === Scenario 2: Read-only queries ===
  tests.push({ id: call('get', { command: 'system.get_info' }), desc: 'get(system.get_info)' });
  tests.push({ id: call('get', { command: 'display.list' }), desc: 'get(display.list)' });
  tests.push({ id: call('get', { command: 'mouse.get' }), desc: 'get(mouse.get) — cursor pos' });
  tests.push({ id: call('get', { command: 'process.list' }), desc: 'get(process.list)' });
  tests.push({ id: call('get', { command: 'window.list' }), desc: 'get(window.list)' });
  tests.push({ id: call('get', { command: 'clipboard.get_text' }), desc: 'get(clipboard.get_text)' });
  tests.push({ id: call('get', { command: 'safety.status' }), desc: 'get(safety.status)' });
  tests.push({ id: call('get', { command: 'safety.action_history' }), desc: 'get(safety.action_history)' });
  tests.push({ id: call('get', { command: 'coord.scale_info', params: JSON.stringify({ titleOrHandle: 'Notepad' }) }), desc: 'get(coord.scale_info) — EXPECTED_FAIL (no Notepad open)' });

  // === Scenario 4: Routing validation ===
  tests.push({ id: call('do', { command: 'window.list' }), desc: 'do(window.list) — SHOULD FAIL (readonly via do)' });
  tests.push({ id: call('get', { command: 'mouse.click', params: JSON.stringify({ x: 0, y: 0 }) }), desc: 'get(mouse.click) — SHOULD FAIL (mutation via get)' });
  tests.push({ id: call('get', { command: 'nonexistent.cmd' }), desc: 'get(nonexistent.cmd) — SHOULD FAIL (unknown)' });
  tests.push({ id: call('do', { command: '' }), desc: 'do("") — SHOULD FAIL (empty command)' });
  tests.push({ id: call('help', { topic: 'nonexistent' }), desc: 'help(nonexistent) — SHOULD FAIL (unknown)' });

  // === Scenario 5: Missing/invalid params ===
  tests.push({ id: call('get', { command: 'file.read' }), desc: 'get(file.read) no params — SHOULD FAIL' });
  tests.push({ id: call('do', { command: 'file.write', params: '{}' }), desc: 'do(file.write) no path — SHOULD FAIL' });
  tests.push({ id: call('do', { command: 'mouse.click', params: 'not-json' }), desc: 'do(mouse.click) bad JSON — SHOULD FAIL' });

  // === Scenario 6: Case insensitive ===
  tests.push({ id: call('get', { command: 'Mouse.Get' }), desc: 'get(Mouse.Get) — case insensitive' });
  tests.push({ id: call('help', { topic: 'MOUSE' }), desc: 'help(MOUSE) — case insensitive' });

  // === Scenario 7: Screen capture ===
  tests.push({ id: call('get', { command: 'screen.capture' }), desc: 'get(screen.capture) — screenshot' });

  // === Scenario 8: OCR ===
  tests.push({ id: call('get', { command: 'ocr.read' }), desc: 'get(ocr.read) — full screen OCR' });

  // === Scenario 9: Report ===
  tests.push({ id: call('get', { command: 'report.get_desktop' }), desc: 'get(report.get_desktop)' });

  // === Scenario 10: Session (fix: path not label) ===
  tests.push({ id: call('do', { command: 'session.save', params: JSON.stringify({ path: 'C:/temp/dispatch-session.json' }) }), desc: 'do(session.save)' });
  tests.push({ id: call('get', { command: 'session.bookmark_list' }), desc: 'get(session.bookmark_list)' });

  // === Scenario 11: Shell ===
  tests.push({ id: call('do', { command: 'shell.execute', params: JSON.stringify({ command: 'echo dispatch-works' }) }), desc: 'do(shell.execute) — echo' });

  // === Scenario 3: File write (first step) ===
  tests.push({ id: call('do', { command: 'file.write', params: JSON.stringify({ path: 'C:/temp/harness-dispatch-test.txt', content: 'Hello from dispatch!' }) }), desc: 'do(file.write)' });
}, 500);

// === Batch 2: File read-back tests (sent at 3000ms — after write completes) ===
setTimeout(() => {
  tests.push({ id: call('get', { command: 'file.read', params: JSON.stringify({ path: 'C:/temp/harness-dispatch-test.txt' }) }), desc: 'get(file.read) — read back' });
  tests.push({ id: call('get', { command: 'file.check', params: JSON.stringify({ path: 'C:/temp/harness-dispatch-test.txt' }) }), desc: 'get(file.check) — exists?' });
  tests.push({ id: call('get', { command: 'file.info', params: JSON.stringify({ path: 'C:/temp/harness-dispatch-test.txt' }) }), desc: 'get(file.info)' });

  // === Desktop (may fail in some environments — test dispatch routing, not runtime) ===
  tests.push({ id: call('get', { command: 'desktop.count' }), desc: 'get(desktop.count) — EXPECTED_FAIL (COM may fail)' });
  tests.push({ id: call('get', { command: 'desktop.current' }), desc: 'get(desktop.current) — EXPECTED_FAIL (COM may fail)' });
}, 3000);

// === Batch 3: Cleanup (sent at 5000ms) ===
setTimeout(() => {
  tests.push({ id: call('do', { command: 'file.delete', params: JSON.stringify({ path: 'C:/temp/harness-dispatch-test.txt' }) }), desc: 'do(file.delete) — cleanup' });
  tests.push({ id: call('do', { command: 'file.delete', params: JSON.stringify({ path: 'C:/temp/dispatch-session.json' }) }), desc: 'do(file.delete) — session cleanup' });
}, 5000);

// === Results (collected at 15000ms) ===
setTimeout(() => {
  const responses = new Map();
  const lines = buf.split('\n').filter(l => l.trim());
  for (const line of lines) {
    try {
      const msg = JSON.parse(line);
      if (msg.id) responses.set(msg.id, msg);
    } catch { }
  }

  let pass = 0, fail = 0, expectedFail = 0;
  for (const t of tests) {
    const r = responses.get(t.id);
    if (r === undefined) { console.log('[TIMEOUT] ' + t.desc); fail++; continue; }

    const text = r.result?.content?.[0]?.text || r.error?.message || '';
    let parsed;
    try { parsed = JSON.parse(text); } catch { parsed = null; }

    const ok = parsed?.ok;
    const isError = parsed?.error || r.error;
    const errCode = parsed?.error?.code || '';
    const errMsg = parsed?.error?.message || r.error?.message || '';

    const shouldFail = t.desc.includes('SHOULD FAIL');
    const expectedToFail = t.desc.includes('EXPECTED_FAIL');

    let status, detail;
    if (shouldFail) {
      if (isError) { status = 'PASS'; detail = errCode + ': ' + errMsg.substring(0, 70); pass++; }
      else { status = 'FAIL'; detail = 'Expected error but got ok=' + ok; fail++; }
    } else if (expectedToFail) {
      // These may fail due to environment (no Notepad, COM issues) — not dispatch bugs
      if (ok === true) { status = 'PASS'; detail = JSON.stringify(parsed.data).substring(0, 90); pass++; }
      else { status = 'SKIP'; detail = (errMsg || r.error?.message || 'env-dependent').substring(0, 70); expectedFail++; }
    } else {
      if (ok === true) {
        const data = parsed.data;
        if (data?.content) detail = data.content.split('\n')[0].substring(0, 80);
        else if (data?.message) detail = data.message;
        else if (data?.items) detail = data.count + ' items';
        else if (data?.result !== undefined) detail = 'result=' + data.result + (data.detail ? ' (' + data.detail + ')' : '');
        else detail = JSON.stringify(data).substring(0, 90);
        status = 'PASS'; pass++;
      } else if (ok === false) {
        status = 'FAIL'; detail = errCode + ': ' + errMsg.substring(0, 70); fail++;
      } else if (r.error) {
        status = 'FAIL'; detail = r.error.message?.substring(0, 70); fail++;
      } else {
        status = 'FAIL'; detail = text.substring(0, 80); fail++;
      }
    }
    console.log('[' + status + '] ' + t.desc);
    console.log('       ' + detail);
  }
  console.log('\n=== ' + pass + ' passed, ' + fail + ' failed, ' + expectedFail + ' skipped (env), ' + tests.length + ' total ===');
  proc.kill();
  process.exit(fail > 0 ? 1 : 0);
}, 15000);
