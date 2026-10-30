// src/app/app.component.ts
import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

const DEMO_NOTICE_KEY = 'wazzuf_demo_notice_dismissed';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    @if (showDemoNotice()) {
      <div class="demo-notice" role="note">
        <span>
          WazzufJobs is currently a demo platform. Please do not upload real CVs or personal documents.
        </span>
        <button type="button" (click)="dismissDemoNotice()" aria-label="Dismiss notice">✕</button>
      </div>
    }
    <router-outlet />
  `,
  styles: [`
    .demo-notice {
      position: fixed;
      left: 0;
      right: 0;
      bottom: 0;
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 8px 44px 8px 16px;
      background: #102c4e;
      color: #ffffff;
      font-size: 13px;
      line-height: 1.4;
      text-align: center;
    }
    .demo-notice button {
      position: absolute;
      right: 12px;
      background: transparent;
      border: 0;
      color: inherit;
      font-size: 14px;
      cursor: pointer;
      padding: 4px 6px;
    }
  `]
})
export class AppComponent {
  readonly showDemoNotice = signal(sessionStorage.getItem(DEMO_NOTICE_KEY) !== '1');

  dismissDemoNotice() {
    sessionStorage.setItem(DEMO_NOTICE_KEY, '1');
    this.showDemoNotice.set(false);
  }
}
