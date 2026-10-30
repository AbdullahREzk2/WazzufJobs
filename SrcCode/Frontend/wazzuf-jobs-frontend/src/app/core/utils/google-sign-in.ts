import { environment } from '../../../environments/environment';

/**
 * Google sign-in is enabled only when a Google OAuth client id is configured for this build
 * (environment.googleClientId). The backend independently refuses Google sign-in when its own
 * Google:ClientId setting is missing, so a client-side misconfiguration cannot enable it.
 */
export function isGoogleSignInEnabled(clientId: string | null | undefined = environment.googleClientId): boolean {
  return typeof clientId === 'string' && clientId.trim().length > 0;
}
