import { describe, test, expect } from 'vitest';
import type { BankProviderInfo, BankProviderAuthModeInfo } from '@/types/bank-connections';

describe('BankProviderInfo auth mode types', () => {
  test('provider with hosted_oauth default advertises both modes', () => {
    const provider: BankProviderInfo = {
      providerId: 'akahu',
      displayName: 'Akahu',
      supportsWebhooks: true,
      supportsBalanceFetch: true,
      supportedAuthModes: [
        { modeId: 'personal_tokens', displayName: 'Personal tokens', requiresUserCredentials: true },
        { modeId: 'hosted_oauth', displayName: 'MyMascada OAuth', requiresUserCredentials: false },
      ],
      defaultAuthMode: 'hosted_oauth',
    };

    expect(provider.supportedAuthModes).toHaveLength(2);
    const oauthMode = provider.supportedAuthModes!.find(m => m.modeId === 'hosted_oauth');
    expect(oauthMode).toBeDefined();
    expect(oauthMode!.requiresUserCredentials).toBe(false);
    expect(provider.defaultAuthMode).toBe('hosted_oauth');
  });

  test('provider without hosted secrets falls back to personal_tokens only', () => {
    const provider: BankProviderInfo = {
      providerId: 'akahu',
      displayName: 'Akahu',
      supportsWebhooks: true,
      supportsBalanceFetch: true,
      supportedAuthModes: [
        { modeId: 'personal_tokens', displayName: 'Personal tokens', requiresUserCredentials: true },
      ],
      defaultAuthMode: 'personal_tokens',
    };

    expect(provider.supportedAuthModes).toHaveLength(1);
    expect(provider.defaultAuthMode).toBe('personal_tokens');
  });

  test('backward compat: provider without auth mode fields still works', () => {
    const provider: BankProviderInfo = {
      providerId: 'akahu',
      displayName: 'Akahu',
      supportsWebhooks: true,
      supportsBalanceFetch: true,
    };

    expect(provider.supportedAuthModes).toBeUndefined();
    expect(provider.defaultAuthMode).toBeUndefined();
  });
});
