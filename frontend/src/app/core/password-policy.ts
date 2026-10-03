import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Same rule as PasswordRules on the API: 8+ characters with a letter and a number. */
export function passwordPolicy(control: AbstractControl): ValidationErrors | null {
  const v: string = control.value ?? '';
  if (v.length < 8) return { policy: 'At least 8 characters.' };
  if (!/[A-Za-z]/.test(v) || !/\d/.test(v)) return { policy: 'Use at least one letter and one number.' };
  return null;
}
