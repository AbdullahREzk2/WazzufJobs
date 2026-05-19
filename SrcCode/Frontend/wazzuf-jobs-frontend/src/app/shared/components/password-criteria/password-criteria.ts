import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { getPasswordCriteriaStatus } from '../../../core/utils/password.validator';

@Component({
  selector: 'app-password-criteria',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './password-criteria.html',
  styleUrls: ['./password-criteria.scss']
})
export class PasswordCriteria {
  @Input() password = '';
  @Input() showWhenEmpty = false;

  get criteria() {
    return getPasswordCriteriaStatus(this.password);
  }

  get visible(): boolean {
    return this.showWhenEmpty || this.password.length > 0;
  }
}
