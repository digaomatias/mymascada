import { useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';

/**
 * Hook that handles automatic token validation retry when the user comes back online
 * or when the window regains focus (e.g., switching browser tabs back)
 */
export function useConnectionRecovery() {
  const { user, retryTokenValidation } = useAuth();

  useEffect(() => {
    // Only add listeners if user should be authenticated but isn't loaded
    const token = typeof window !== 'undefined' ? localStorage.getItem('auth_token') : null;
    
    if (token && !user) {
      const handleOnline = () => {
        console.log('Connection restored, retrying token validation');
        retryTokenValidation();
      };

      const handleFocus = () => {
        console.log('Window focused, checking token validation');
        retryTokenValidation();
      };

      // Listen for network reconnection
      window.addEventListener('online', handleOnline);
      
      // Listen for window focus (user coming back to tab)
      window.addEventListener('focus', handleFocus);

      return () => {
        window.removeEventListener('online', handleOnline);
        window.removeEventListener('focus', handleFocus);
      };
    }
  }, [user, retryTokenValidation]);
}