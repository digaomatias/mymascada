'use client';

import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { UserDto, LoginRequest, RegisterRequest, AuthenticationResponse } from '@/types/auth';
import { apiClient } from '@/lib/api-client';

interface AuthContextType {
  user: UserDto | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (credentials: LoginRequest) => Promise<AuthenticationResponse>;
  register: (userData: RegisterRequest) => Promise<AuthenticationResponse>;
  loginWithToken: (token: string) => Promise<boolean>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  retryTokenValidation: () => Promise<void>;
  refreshAccessToken: () => Promise<boolean>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasToken, setHasToken] = useState(false);

  const isAuthenticated = !!user && hasToken;

  // Initialize auth state
  useEffect(() => {
    if (typeof window === 'undefined') {
      setIsLoading(false);
      return;
    }

    const initializeAuth = async () => {
      try {
        const token = apiClient.getToken();
        setHasToken(!!token);
        if (token) {
          // First check if token is structurally valid and not expired
          if (isTokenStructurallyValid(token)) {
            // Validate token by fetching current user
            try {
              const userData = await apiClient.getCurrentUser();
              setUser(userData as UserDto);
            } catch (error: unknown) {
              console.error('Token validation failed:', error);
              // Only clear token on actual authentication errors (401), not network errors
              const errorWithStatus = error as { status?: number; response?: { status?: number } };
              if (errorWithStatus?.status === 401 || errorWithStatus?.response?.status === 401) {
                console.log('Access token is invalid (401), trying refresh...');
                // Try to refresh the token before clearing everything
                try {
                  const response = await apiClient.refreshToken();
                  if (response.isSuccess && response.token && response.user) {
                    console.log('Token refreshed successfully during initialization');
                    apiClient.setToken(response.token);
                    setUser(response.user);
                    setHasToken(true);
                  } else {
                    console.log('Refresh failed during initialization, clearing token');
                    apiClient.removeToken();
                    setUser(null);
                    setHasToken(false);
                  }
                } catch {
                  console.log('Refresh token is invalid, clearing everything');
                  apiClient.removeToken();
                  setUser(null);
                  setHasToken(false);
                }
              } else {
                console.log('Network or server error during token validation, keeping token for retry');
                // For network errors, we'll keep the token but set user to null
                // This prevents logout on temporary network issues
                setUser(null);
              }
            }
          } else {
            console.log('Token is expired or malformed, clearing token');
            apiClient.removeToken();
            setUser(null);
            setHasToken(false);
          }
        }
      } catch (error) {
        console.error('Failed to initialize auth:', error);
        // Don't automatically clear token for unexpected errors
        setUser(null);
        setHasToken(false);
      } finally {
        setIsLoading(false);
      }
    };

    initializeAuth();
  }, []);

  // Helper function to check if token is structurally valid and not expired
  const isTokenStructurallyValid = (token: string): boolean => {
    try {
      // Basic JWT structure check (3 parts separated by dots)
      const parts = token.split('.');
      if (parts.length !== 3) {
        return false;
      }

      // Try to decode the payload to check expiration
      const payload = JSON.parse(atob(parts[1]));
      if (payload.exp) {
        // Check if token is expired (exp is in seconds, Date.now() is in milliseconds)
        const isExpired = payload.exp * 1000 < Date.now();
        if (isExpired) {
          console.log('Token is expired');
          return false;
        }
      }

      return true;
    } catch (error) {
      console.error('Error validating token structure:', error);
      return false;
    }
  };

  const login = async (credentials: LoginRequest): Promise<AuthenticationResponse> => {
    try {
      setIsLoading(true);
      const response = await apiClient.login(credentials);
      
      if (response.isSuccess && response.token && response.user) {
        apiClient.setToken(response.token);
        setUser(response.user);
        setHasToken(true);
      }
      
      return response;
    } catch (error) {
      console.error('Login failed:', error);
      const authError = error as { authResponse?: AuthenticationResponse, message?: string };
      // If it's an auth error with response structure, return it
      if (authError.authResponse) {
        return authError.authResponse;
      }
      // Otherwise, create a proper auth response format
      return {
        isSuccess: false,
        errors: [authError.message || 'An unexpected error occurred. Please try again.']
      };
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (userData: RegisterRequest): Promise<AuthenticationResponse> => {
    try {
      setIsLoading(true);
      const response = await apiClient.register(userData);
      
      if (response.isSuccess && response.token && response.user) {
        apiClient.setToken(response.token);
        setUser(response.user);
        setHasToken(true);
      }
      
      return response;
    } catch (error) {
      console.error('Registration failed:', error);
      const authError = error as { authResponse?: AuthenticationResponse, message?: string };
      // If it's an auth error with response structure, return it
      if (authError.authResponse) {
        return authError.authResponse;
      }
      // Otherwise, create a proper auth response format
      return {
        isSuccess: false,
        errors: [authError.message || 'An unexpected error occurred. Please try again.']
      };
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    try {
      // Revoke refresh token on the server
      await apiClient.revokeToken();
    } catch (error) {
      console.error('Failed to revoke refresh token:', error);
      // Continue with logout even if revoke fails
    }
    
    apiClient.removeToken();
    setUser(null);
    setHasToken(false);
  };

  const loginWithToken = async (token: string): Promise<boolean> => {
    try {
      setIsLoading(true);
      apiClient.setToken(token);
      
      // Validate token by fetching current user
      const userData = await apiClient.getCurrentUser();
      setUser(userData as UserDto);
      setHasToken(true);
      
      return true;
    } catch (error) {
      console.error('Token login failed:', error);
      apiClient.removeToken();
      setUser(null);
      setHasToken(false);
      return false;
    } finally {
      setIsLoading(false);
    }
  };

  const refreshUser = async () => {
    try {
      const userData = await apiClient.getCurrentUser();
      setUser(userData as UserDto);
    } catch (error: unknown) {
      console.error('Failed to refresh user:', error);
      const errorWithStatus = error as { status?: number; response?: { status?: number } };
      if (errorWithStatus?.status === 401 || errorWithStatus?.response?.status === 401) {
        console.log('Access token invalid during user refresh, trying refresh token...');
        const refreshSuccess = await refreshAccessToken();
        if (!refreshSuccess) {
          console.log('Refresh failed during user refresh, logging out');
          await logout();
        }
      } else {
        // For other errors, don't automatically log out
        console.log('Non-auth error during user refresh, keeping session');
      }
    }
  };

  const retryTokenValidation = async (): Promise<void> => {
    const token = apiClient.getToken();
    if (token && isTokenStructurallyValid(token)) {
      try {
        const userData = await apiClient.getCurrentUser();
        setUser(userData as UserDto);
        console.log('Token validation retry successful');
      } catch (error: unknown) {
        console.error('Token validation retry failed:', error);
        const errorWithStatus = error as { status?: number; response?: { status?: number } };
        if (errorWithStatus?.status === 401 || errorWithStatus?.response?.status === 401) {
          console.log('Access token invalid, trying refresh...');
          const refreshSuccess = await refreshAccessToken();
          if (!refreshSuccess) {
            console.log('Refresh failed, logging out');
            await logout();
          }
        }
      }
    }
  };

  const refreshAccessToken = async (): Promise<boolean> => {
    try {
      console.log('Attempting to refresh access token...');
      const response = await apiClient.refreshToken();
      
      if (response.isSuccess && response.token && response.user) {
        console.log('Access token refreshed successfully');
        apiClient.setToken(response.token);
        setUser(response.user);
        return true;
      } else {
        console.log('Refresh token response was not successful');
        return false;
      }
    } catch (error: unknown) {
      console.error('Failed to refresh access token:', error);
      const errorWithStatus = error as { status?: number; response?: { status?: number } };
      if (errorWithStatus?.status === 401 || errorWithStatus?.response?.status === 401) {
        console.log('Refresh token is invalid or expired');
      }
      return false;
    }
  };

  const value: AuthContextType = {
    user,
    isLoading,
    isAuthenticated,
    login,
    register,
    loginWithToken,
    logout,
    refreshUser,
    retryTokenValidation,
    refreshAccessToken,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
