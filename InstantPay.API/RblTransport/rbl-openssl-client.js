'use strict';

const fs = require('fs');
const https = require('https');

let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
  try {
    const cfg = JSON.parse(input);
    const target = new URL(cfg.url);
    if (target.protocol !== 'https:' || target.hostname !== 'gateway.rbl.bank.in') {
      throw new Error('RBL transport target is not allowed');
    }

    const request = https.request({
      protocol: target.protocol,
      hostname: target.hostname,
      port: target.port || 443,
      // RBL Developer Portal registrations are tied to the public IPv4 address.
      // Avoid Node selecting an IPv6 route that differs from Postman's egress.
      family: 4,
      path: `${target.pathname}${target.search}`,
      method: 'POST',
      pfx: fs.readFileSync(cfg.certificatePath),
      passphrase: cfg.certificatePassword,
      minVersion: 'TLSv1.2',
      maxVersion: 'TLSv1.2',
      rejectUnauthorized: true,
      auth: `${cfg.username}:${cfg.password}`,
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(cfg.body),
        'User-Agent': 'InstantPayment-RBL/1.0'
      },
      timeout: cfg.timeoutMs || 90000
    }, response => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', chunk => body += chunk);
      response.on('end', () => process.stdout.write(JSON.stringify({ statusCode: response.statusCode || 0, body })));
    });
    request.on('timeout', () => request.destroy(new Error('RBL request timed out')));
    request.on('error', error => { process.stderr.write(error.stack || error.message); process.exitCode = 2; });
    request.end(cfg.body);
  } catch (error) {
    process.stderr.write(error.stack || error.message);
    process.exitCode = 1;
  }
});
