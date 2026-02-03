/**
 * Navigation utilities for preserving context when navigating between pages
 */

/**
 * Creates a transaction detail URL with return context preserved
 */
export function createTransactionDetailUrl(
  transactionId: number | string, 
  currentUrl?: string
): string {
  const baseUrl = `/transactions/${transactionId}`;
  
  if (!currentUrl) {
    return baseUrl;
  }
  
  // Encode the return URL to safely pass it as a query parameter
  const returnUrl = encodeURIComponent(currentUrl);
  return `${baseUrl}?returnUrl=${returnUrl}`;
}

/**
 * Creates a transaction edit URL with return context preserved
 */
export function createTransactionEditUrl(
  transactionId: number | string, 
  currentUrl?: string
): string {
  const baseUrl = `/transactions/${transactionId}/edit`;
  
  if (!currentUrl) {
    return baseUrl;
  }
  
  const returnUrl = encodeURIComponent(currentUrl);
  return `${baseUrl}?returnUrl=${returnUrl}`;
}

/**
 * Gets the return URL from query parameters or provides a sensible fallback
 */
export function getReturnUrl(searchParams: URLSearchParams): string {
  const returnUrl = searchParams.get('returnUrl');
  
  if (returnUrl) {
    try {
      return decodeURIComponent(returnUrl);
    } catch {
      console.warn('Failed to decode return URL:', returnUrl);
    }
  }
  
  // Fallback to transactions page
  return '/transactions';
}

/**
 * Gets the current page URL with all search parameters
 * Useful for creating return URLs
 */
export function getCurrentPageUrl(pathname: string, searchParams: URLSearchParams): string {
  const params = searchParams.toString();
  return params ? `${pathname}?${params}` : pathname;
}

/**
 * Determines if the current page is a "source" page that should be preserved as context
 */
export function isSourcePage(pathname: string): boolean {
  return (
    pathname === '/transactions' ||
    pathname.startsWith('/accounts') ||
    pathname.startsWith('/categories') ||
    pathname.startsWith('/reports') ||
    pathname === '/transactions/categorize'
  );
}

/**
 * Gets a human-readable label for the return URL context
 */
export function getReturnUrlLabel(returnUrl: string): string {
  try {
    const url = new URL(returnUrl, 'http://localhost');
    const pathname = url.pathname;
    const searchParams = url.searchParams;
    
    // Generate context-aware labels
    if (pathname === '/transactions') {
      const filters = [];
      if (searchParams.get('search')) filters.push('search results');
      if (searchParams.get('account')) filters.push('filtered by account');
      if (searchParams.get('category')) filters.push('filtered by category');
      const page = searchParams.get('page');
      if (page && page !== '1') filters.push(`page ${page}`);
      
      if (filters.length > 0) {
        return `Transactions (${filters.join(', ')})`;
      }
      return 'Transactions';
    }
    
    if (pathname.startsWith('/accounts/')) {
      const accountId = pathname.split('/')[2];
      return `Account Details${accountId ? ` (${accountId})` : ''}`;
    }
    
    if (pathname.startsWith('/categories/')) {
      return 'Category Details';
    }
    
    if (pathname.startsWith('/reports/')) {
      return 'Reports';
    }
    
    if (pathname === '/transactions/categorize') {
      return 'Transaction Categorization';
    }
    
    // Generic fallback
    return 'Previous Page';
  } catch {
    return 'Previous Page';
  }
}