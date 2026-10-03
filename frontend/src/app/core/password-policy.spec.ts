import { FormControl } from '@angular/forms';
import { passwordPolicy } from './password-policy';

describe('passwordPolicy', () => {
  const check = (v: string) => passwordPolicy(new FormControl(v));

  it('accepts 8+ characters with a letter and a number', () => {
    expect(check('Passw0rd')).toBeNull();
  });

  it('rejects short passwords', () => {
    expect(check('Ab1')?.['policy']).toContain('8');
  });

  it('needs both a letter and a number', () => {
    expect(check('abcdefgh')).not.toBeNull();
    expect(check('12345678')).not.toBeNull();
  });
});
