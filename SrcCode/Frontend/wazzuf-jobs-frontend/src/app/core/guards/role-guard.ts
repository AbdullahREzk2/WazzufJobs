// src/app/core/guards/role-guard.ts
import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService  = inject(AuthService);
  const router       = inject(Router);
  const requiredRole = route.data['role'] as string;

  if (requiredRole === 'Admin' && authService.isAdmin()) return true;
  if (requiredRole === 'User'  && authService.isUser() && !authService.isAdmin()) return true;

  router.navigate(['/']);
  return false;
};