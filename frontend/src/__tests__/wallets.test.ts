import { describe, test, expect, vi, beforeEach, afterEach } from 'vitest';

// We test the ApiClient's wallet methods by instantiating a fresh client and
// mocking global.fetch.  The ApiClient constructor reads
// NEXT_PUBLIC_API_URL which falls back to 'http://localhost:5126'.

/** The fallback base URL used by ApiClient when NEXT_PUBLIC_API_URL is not set. */
const API_CLIENT_BASE_URL = 'http://localhost:5126';

// Dynamic import so we can reset module-level state between tests.
async function createApiClient() {
  // Force-reimport to get a fresh class (not the singleton)
  const mod = await import('@/lib/api-client');
  // The module exports `apiClient` as a singleton.  For unit-testing we need
  // a plain instance so we can control fetch.  The class is *not* exported
  // directly, but we can reach it via the singleton and its constructor.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const client = Object.create(Object.getPrototypeOf(mod.apiClient));
  // Re-run the constructor body manually
  client.baseURL = API_CLIENT_BASE_URL;
  client.refreshPromise = null;
  return client;
}

describe('ApiClient wallet methods', () => {
  let client: Awaited<ReturnType<typeof createApiClient>>;
  const mockFetch = vi.fn();

  beforeEach(async () => {
    vi.stubGlobal('fetch', mockFetch);
    // Mock localStorage for token management
    const store: Record<string, string> = {};
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store[key] ?? null,
      setItem: (key: string, value: string) => { store[key] = value; },
      removeItem: (key: string) => { delete store[key]; },
    });
    client = await createApiClient();
    mockFetch.mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test('getWallets() calls correct URL with GET', async () => {
    const wallets = [
      { id: 1, name: 'Vacation', balance: 500, currency: 'USD' },
      { id: 2, name: 'Emergency', balance: 1000, currency: 'USD' },
    ];

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify(wallets)),
    });

    const result = await client.getWallets();

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe(`${API_CLIENT_BASE_URL}/api/latest/wallets`);
    expect(options.method).toBeUndefined(); // defaults to GET when no method specified in getWallets
    expect(result).toEqual(wallets);
  });

  test('getWallets() with includeArchived param appends query string', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify([])),
    });

    await client.getWallets({ includeArchived: true });

    const [url] = mockFetch.mock.calls[0];
    expect(url).toContain('includeArchived=true');
  });

  test('createWallet() calls correct URL with POST and body', async () => {
    const walletRequest = {
      name: 'New Wallet',
      icon: 'star',
      color: '#FF0000',
      currency: 'EUR',
    };

    const createdWallet = { id: 5, ...walletRequest, balance: 0 };

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      text: () => Promise.resolve(JSON.stringify(createdWallet)),
    });

    const result = await client.createWallet(walletRequest);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe(`${API_CLIENT_BASE_URL}/api/latest/wallets`);
    expect(options.method).toBe('POST');
    expect(JSON.parse(options.body)).toEqual(walletRequest);
    expect(result).toEqual(createdWallet);
  });

  test('deleteWallet() calls correct URL with DELETE', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      text: () => Promise.resolve(''),
    });

    await client.deleteWallet(3);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe(`${API_CLIENT_BASE_URL}/api/latest/wallets/3`);
    expect(options.method).toBe('DELETE');
  });

  test('createWalletAllocation() calls correct URL with POST', async () => {
    const allocationRequest = {
      transactionId: 10,
      amount: 75.50,
      note: 'Partial allocation',
    };

    const createdAllocation = { id: 100, ...allocationRequest };

    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      text: () => Promise.resolve(JSON.stringify(createdAllocation)),
    });

    const result = await client.createWalletAllocation(1, allocationRequest);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe(`${API_CLIENT_BASE_URL}/api/latest/wallets/1/allocations`);
    expect(options.method).toBe('POST');
    expect(JSON.parse(options.body)).toEqual(allocationRequest);
    expect(result).toEqual(createdAllocation);
  });

  test('deleteWalletAllocation() calls correct URL with DELETE', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      text: () => Promise.resolve(''),
    });

    await client.deleteWalletAllocation(2, 50);

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe(`${API_CLIENT_BASE_URL}/api/latest/wallets/2/allocations/50`);
    expect(options.method).toBe('DELETE');
  });
});
