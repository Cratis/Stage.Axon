// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

if (process.argv.length !== 4 || !process.env.JAVA_HOME) throw new Error('Usage: boot-smoke.mjs <corpus-directory> <log-directory>; JAVA_HOME is required');
const root = path.resolve(process.argv[2]);
const logs = path.resolve(process.argv[3]);
const applications = JSON.parse(fs.readFileSync(path.join(root, 'applications.json'), 'utf8'));
if (applications.length !== 23 || new Set(applications).size !== 23) throw new Error('Expected 23 distinct generated applications');
fs.mkdirSync(logs, { recursive: true });
let current;
for (const signal of ['SIGTERM', 'SIGINT']) {
    process.on(signal, () => { current?.kill(signal); process.exit(signal === 'SIGTERM' ? 143 : 130); });
}
for (const name of applications) {
    if (!/^[a-z0-9-]+$/.test(name)) throw new Error(`Invalid application name: ${name}`);
    const server = net.createServer();
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    const port = server.address().port;
    await new Promise(resolve => server.close(resolve));
    const target = path.join(root, name, 'target');
    const jars = fs.readdirSync(target).filter(file => file.endsWith('.jar'));
    if (jars.length !== 1) throw new Error(`${name}: expected one packaged application`);
    const fd = fs.openSync(path.join(logs, `${name}.log`), 'w');
    const child = spawn(path.join(process.env.JAVA_HOME, 'bin/java'), ['-Xmx256m', '-jar', path.join(target, jars[0]), `--server.port=${port}`], { stdio: ['ignore', fd, fd] });
    current = child;
    fs.closeSync(fd);
    let stopped = false;
    const exited = new Promise((resolve, reject) => {
        child.once('exit', code => { stopped = true; resolve(code); });
        child.once('error', reject);
    });
    try {
        const deadline = Date.now() + 45000;
        let status;
        while (!stopped && Date.now() < deadline) {
            try {
                const response = await fetch(`http://127.0.0.1:${port}/`, { signal: AbortSignal.timeout(2000) });
                status = response.status;
                await response.body?.cancel();
                break;
            } catch { await delay(200); }
        }
        if (status !== 404) throw new Error(`${name}: expected intentional root HTTP 404 from a booted application, got ${status ?? 'no response'}`);
        console.log(`${name}: packaged application boot and HTTP smoke passed`);
    } finally {
        child.kill('SIGTERM');
        await Promise.race([exited, delay(5000)]);
        if (!stopped) { child.kill('SIGKILL'); await exited; }
        current = undefined;
    }
}
console.log(`PASS: all ${applications.length} applications booted and answered HTTP; all owned processes stopped.`);
