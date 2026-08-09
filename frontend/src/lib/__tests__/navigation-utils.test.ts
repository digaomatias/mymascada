import { describe, expect, it } from 'vitest';
import { getReturnUrl, sanitizeInternalUrl } from '../navigation-utils';

describe('sanitizeInternalUrl', () => {
  it.each([
    '/transactions',
    '/transactions?page=2&search=coffee',
    '/accounts/12/edit',
  ])('allows internal path %s', (url) => {
    expect(sanitizeInternalUrl(url)).toBe(url);
  });

  it.each([
    'https://evil.com',
    'http://evil.com/transactions',
    '//evil.com',
    '/\\evil.com',
    'javascript:alert(1)',
    // eslint-disable-next-line no-script-url
    'javascript://%0aalert(1)',
    'data:text/html,<script>alert(1)</script>',
    'transactions', // relative, not rooted
    '',
  ])('rejects unsafe url %s', (url) => {
    expect(sanitizeInternalUrl(url)).toBe('/transactions');
  });

  it('uses the provided fallback for unsafe urls', () => {
    expect(sanitizeInternalUrl('https://evil.com', '/dashboard')).toBe('/dashboard');
  });
});

describe('getReturnUrl', () => {
  it('returns a safe internal returnUrl', () => {
    const params = new URLSearchParams({ returnUrl: encodeURIComponent('/transactions?page=3') });
    expect(getReturnUrl(params)).toBe('/transactions?page=3');
  });

  it('falls back when returnUrl is an external url', () => {
    const params = new URLSearchParams({ returnUrl: encodeURIComponent('https://evil.com/phish') });
    expect(getReturnUrl(params)).toBe('/transactions');
  });

  it('falls back when returnUrl is a javascript: url', () => {
    const params = new URLSearchParams({ returnUrl: encodeURIComponent('javascript:alert(1)') });
    expect(getReturnUrl(params)).toBe('/transactions');
  });

  it('falls back when returnUrl is missing', () => {
    expect(getReturnUrl(new URLSearchParams())).toBe('/transactions');
  });
});
