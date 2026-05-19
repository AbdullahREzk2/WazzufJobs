import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Keeps admins in the admin area — job seeker routes are for applicants only. */
export const seekerGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  if (auth.isAdmin()) {
    router.navigate(['/admin']);
    return false;
  }
  return true;
};
