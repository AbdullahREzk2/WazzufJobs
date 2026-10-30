import { HttpErrorResponse } from '@angular/common/http';

/** Reads the message from the API's ProblemDetails ({ errors: [code, message] }) or falls back. */
export function apiErrorMessage(err: HttpErrorResponse, fallback: string): string {
  const errors = err?.error?.errors;
  if (Array.isArray(errors) && typeof errors[1] === 'string') return errors[1];
  return err?.error?.detail || fallback;
}
