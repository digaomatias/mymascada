import { test, expect } from '@playwright/test';

test.describe('Backend Connectivity', () => {
  test('should connect to backend API health endpoint', async ({ request }) => {
    const response = await request.get('https://localhost:5126/api/auth/health', {
      ignoreHTTPSErrors: true,
    });
    
    expect(response.status()).toBe(200);
    console.log('✅ Backend health check passed:', await response.text());
  });

  test('should be able to register and login via API', async ({ request }) => {
    const testUser = {
      email: `test-${Date.now()}@example.com`,
      username: `test-${Date.now()}`,
      firstName: 'Test',
      lastName: 'User',
      password: 'SecurePass123!',
      confirmPassword: 'SecurePass123!'
    };

    // Register user
    const registerResponse = await request.post('https://localhost:5126/api/auth/register', {
      data: testUser,
      ignoreHTTPSErrors: true,
    });

    if (!registerResponse.ok()) {
      const errorText = await registerResponse.text();
      console.log('Register response:', errorText);
    }

    expect(registerResponse.status()).toBe(200);
    const registerData = await registerResponse.json();
    expect(registerData.isSuccess).toBe(true);
    expect(registerData.token).toBeDefined();

    console.log('✅ Registration successful');

    // Login with same user
    const loginResponse = await request.post('https://localhost:5126/api/auth/login', {
      data: {
        email: testUser.email,
        password: testUser.password
      },
      ignoreHTTPSErrors: true,
    });

    expect(loginResponse.status()).toBe(200);
    const loginData = await loginResponse.json();
    expect(loginData.isSuccess).toBe(true);
    expect(loginData.token).toBeDefined();

    console.log('✅ Login successful');
  });
});