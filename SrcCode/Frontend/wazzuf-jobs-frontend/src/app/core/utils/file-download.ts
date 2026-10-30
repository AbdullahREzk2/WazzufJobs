import { HttpResponse } from '@angular/common/http';

/**
 * Saves a blob returned by an authorized API endpoint as a file download.
 * The file is never rendered inside the application origin.
 */
export function saveBlobResponse(response: HttpResponse<Blob>, fallbackName: string) {
  const blob = response.body;
  if (!blob) return;

  const disposition = response.headers.get('content-disposition') ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1]) : fallbackName;

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
