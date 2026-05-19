/** Matches WazzufJobs.BLL Abstractions.Consts.RegexPatterns.Password */
export const PASSWORD_REGEX =
  /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_\-+=[\]{}<>?~]).{8,}$/;

export const PASSWORD_REQUIREMENTS_MESSAGE =
  'Password must be at least 8 characters and include uppercase, lowercase, a number, and a special character (!@#$%^&*()_-+=<>?{}[]~).';

export interface PasswordCriterion {
  id: string;
  label: string;
  test: (value: string) => boolean;
}

export const PASSWORD_CRITERIA: PasswordCriterion[] = [
  { id: 'length', label: 'At least 8 characters', test: v => v.length >= 8 },
  { id: 'lower', label: 'One lowercase letter', test: v => /[a-z]/.test(v) },
  { id: 'upper', label: 'One uppercase letter', test: v => /[A-Z]/.test(v) },
  { id: 'digit', label: 'One number', test: v => /\d/.test(v) },
  {
    id: 'special',
    label: 'One special character (!@#$…)',
    test: v => /[!@#$%^&*()_\-+=[\]{}<>?~]/.test(v)
  }
];

export function isPasswordValid(password: string): boolean {
  return PASSWORD_REGEX.test(password);
}

export function getPasswordCriteriaStatus(password: string) {
  return PASSWORD_CRITERIA.map(c => ({
    ...c,
    met: c.test(password)
  }));
}

export function validatePasswordPair(
  password: string,
  confirmPassword: string
): string | null {
  if (!password || !confirmPassword) {
    return 'Please fill in both password fields.';
  }
  if (!isPasswordValid(password)) {
    return PASSWORD_REQUIREMENTS_MESSAGE;
  }
  if (password !== confirmPassword) {
    return 'Passwords do not match.';
  }
  return null;
}
