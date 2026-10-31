import { chromium, firefox, webkit } from 'playwright';
for (const [name, engine] of [['chromium', chromium], ['firefox', firefox], ['webkit', webkit]]) {
  const browser = await engine.launch();
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('https://wj-auth-probe.vercel.app/selftest.html');
  await page.click('#run'); await page.waitForSelector('#summary:not(:empty)');
  const step1 = (await page.textContent('#out')).replace(/\s+/g, ' ').trim();
  await page.goto('https://wj-auth-probe-xsite.vercel.app/selftest.html');
  await page.click('#run'); await page.waitForSelector('#summary:not(:empty)');
  const step2 = (await page.textContent('#out')).replace(/\s+/g, ' ').trim();
  console.log(`${name.padEnd(9)} same-site: ${step1}\n${''.padEnd(9)} cross-site: ${step2}`);
  await browser.close();
}
