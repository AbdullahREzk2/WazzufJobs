// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth-guard';
import { roleGuard } from './core/guards/role-guard';
import { seekerGuard } from './core/guards/seeker-guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/landing/landing.component')
        .then(m => m.LandingComponent)
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login')
        .then(m => m.Login)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register')
        .then(m => m.Register)
  },
  {
    path: 'confirm-email',
    loadComponent: () =>
      import('./features/auth/confirm-email/confirm-email')
        .then(m => m.ConfirmEmail)
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password')
        .then(m => m.ResetPassword)
  },
  {
    path: 'forget-password',
    loadComponent: () =>
      import('./features/auth/forget-password/forget-password')
        .then(m => m.ForgetPassword)
  },
  {
    path: 'jobs',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/jobs/job-list/job-list')
        .then(m => m.JobList)
  },
  {
    path: 'jobs/:id',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/jobs/job-detail/job-detail')
        .then(m => m.JobDetail)
  },
  {
    path: 'dashboard',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/dashboard/dashboard')
        .then(m => m.Dashboard)
  },
  {
    path: 'profile',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/profile/profile')
        .then(m => m.Profile)
  },
  {
    path: 'onboarding',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/onboarding/onboarding')
        .then(m => m.Onboarding)
  },
  {
    path: 'saved-jobs',
    canActivate: [authGuard, seekerGuard],
    loadComponent: () =>
      import('./features/saved-jobs/saved-jobs')
        .then(m => m.SavedJobs)
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { role: 'Admin' },
    loadComponent: () =>
      import('./shared/layouts/admin-layout/admin-layout')
        .then(m => m.AdminLayout),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/admin/admin-dashboard/admin-dashboard')
            .then(m => m.AdminDashboard)
      },
      {
        path: 'jobs',
        loadComponent: () =>
          import('./features/admin/manage-jobs/manage-jobs')
            .then(m => m.ManageJobs)
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/admin/manage-categories/manage-categories')
            .then(m => m.ManageCategories)
      },
      {
        path: 'applications',
        loadComponent: () =>
          import('./features/admin/view-applications/view-applications')
            .then(m => m.ViewApplications)
      },
      {
        path: 'users',
        loadComponent: () =>
          import('./features/admin/manage-users/manage-users').then(
            m => m.ManageUsers
          )
      },
      {
        path: 'roles',
        loadComponent: () =>
          import('./features/admin/manage-roles/manage-roles').then(
            m => m.ManageRoles
          )
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
