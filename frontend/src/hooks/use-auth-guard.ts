'use client';

import { useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useAuth } from '@/contexts/auth-context';

/**
 * Replaces scattered auth-check + redirect patterns.
 * Key difference: returns shouldRender: true even during auth loading,
 * so page shells render immediately instead of showing a full-page spinner.
 */
export function useAuthGuard() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();

  // Don't redirect to login while an OAuth code exchange is pending
  const hasPendingOAuthCode = searchParams.get('code') !== null;

  useEffect(() => {
    if (!isLoading && !isAuthenticated && !hasPendingOAuthCode) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, hasPendingOAuthCode, router]);

  return {
    /** true during auth loading, OAuth code exchange, AND when authenticated — only false during redirect */
    shouldRender: isLoading || isAuthenticated || hasPendingOAuthCode,
    /** true only after auth is confirmed */
    isAuthResolved: !isLoading && isAuthenticated,
  };
}
