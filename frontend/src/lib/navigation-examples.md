# Navigation Context System Usage Examples

This document provides comprehensive examples of how to use the new navigation context system in the MyMascada finance app.

## Overview

The navigation context system solves the complex navigation problem where users can reach transaction details from multiple contexts (transactions page, account details, reports, etc.) and need intelligent back navigation that returns them to their original context with preserved filters and pagination.

## Core Components

### 1. Navigation Context Hook (`useNavigationContext`)

```typescript
import { useNavigationContext } from '@/hooks/use-navigation-context';

function MyComponent() {
  const { 
    navigateBack, 
    getBackInfo, 
    navigateToTransaction,
    navigateToAccount,
    navigateToCategory,
    isDetailPage,
    isSourcePage 
  } = useNavigationContext();
  
  // Get information about where we came from
  const backInfo = getBackInfo(); // { url: '/transactions?page=2', label: 'Transactions (page 2)' }
  
  // Navigate back to previous context
  const handleBack = () => {
    navigateBack(); // Returns true if successful, false if fallback used
  };
  
  // Navigate to a transaction while preserving current context
  const handleViewTransaction = (id: number) => {
    navigateToTransaction(id); // Automatically saves current context if on source page
  };
}
```

### 2. Contextual Link Components

```typescript
import { 
  ContextualLink, 
  TransactionLink, 
  AccountLink, 
  CategoryLink 
} from '@/components/ui/contextual-link';

function TransactionListItem({ transaction }) {
  return (
    <div>
      {/* Generic contextual link */}
      <ContextualLink href={`/transactions/${transaction.id}`}>
        View Transaction
      </ContextualLink>
      
      {/* Specialized transaction link */}
      <TransactionLink transactionId={transaction.id}>
        View Details
      </TransactionLink>
      
      {/* Account link */}
      <AccountLink accountId={transaction.accountId}>
        View Account
      </AccountLink>
      
      {/* Disable context preservation for specific links */}
      <ContextualLink 
        href="/transactions/new" 
        preserveContext={false}
      >
        Add New Transaction
      </ContextualLink>
    </div>
  );
}
```

### 3. Smart Back Button Components

```typescript
import { 
  SmartBackButton, 
  TransactionBackButton, 
  AccountBackButton,
  CategoryBackButton 
} from '@/components/ui/smart-back-button';

function TransactionDetailPage() {
  return (
    <div>
      {/* Shows contextual label like "Back to Transactions (page 2, filtered by account)" */}
      <TransactionBackButton />
      
      {/* Generic smart back button with custom fallback */}
      <SmartBackButton 
        fallbackUrl="/transactions"
        fallbackLabel="Back to Transactions"
        variant="secondary"
        size="sm"
      />
      
      {/* Hide label on mobile, show icon only */}
      <SmartBackButton showLabel={false} />
    </div>
  );
}
```

### 4. Breadcrumb Navigation

```typescript
import { 
  Breadcrumbs, 
  TransactionBreadcrumbs,
  AccountBreadcrumbs,
  CategoryBreadcrumbs 
} from '@/components/ui/breadcrumbs';

function TransactionDetailPage({ transaction }) {
  return (
    <div>
      {/* Automatic breadcrumbs for transactions */}
      <TransactionBreadcrumbs 
        transactionId={transaction.id}
        transactionDescription={transaction.description}
      />
      
      {/* Custom breadcrumbs */}
      <Breadcrumbs
        items={[
          { label: 'Reports', href: '/reports' },
          { label: 'Monthly Spending', href: '/reports/monthly' },
          { label: 'January 2024', current: true }
        ]}
      />
    </div>
  );
}
```

## Common Usage Patterns

### Pattern 1: Transaction List Component

```typescript
function TransactionList() {
  const { navigateToTransaction } = useNavigationContext();
  
  const handleTransactionClick = (transactionId: number) => {
    // This automatically preserves current context if we're on a source page
    navigateToTransaction(transactionId);
  };
  
  return (
    <div>
      {transactions.map(transaction => (
        <div key={transaction.id}>
          {/* Method 1: Using click handler */}
          <div onClick={() => handleTransactionClick(transaction.id)}>
            {transaction.description}
          </div>
          
          {/* Method 2: Using contextual link */}
          <TransactionLink transactionId={transaction.id}>
            View Details
          </TransactionLink>
        </div>
      ))}
    </div>
  );
}
```

### Pattern 2: Account Details Page

```typescript
function AccountDetailsPage({ accountId }) {
  return (
    <div>
      {/* Breadcrumbs showing navigation path */}
      <AccountBreadcrumbs 
        accountId={accountId}
        accountName={account?.name}
      />
      
      {/* Account details */}
      <div>...</div>
      
      {/* Transaction list for this account */}
      <TransactionList 
        defaultAccountId={accountId}
        showFilters={true}
        // When users click on transactions from here, 
        // they'll be able to come back to this account page
      />
    </div>
  );
}
```

### Pattern 3: Report Pages

```typescript
function MonthlyReportPage() {
  const { navigateToTransaction } = useNavigationContext();
  
  return (
    <div>
      {/* Custom breadcrumbs for report */}
      <Breadcrumbs
        items={[
          { label: 'Reports', href: '/reports' },
          { label: 'Monthly Report', current: true }
        ]}
      />
      
      {/* Report content with transaction links */}
      <div>
        {reportData.transactions.map(transaction => (
          <TransactionLink 
            key={transaction.id} 
            transactionId={transaction.id}
          >
            {transaction.description}
          </TransactionLink>
        ))}
      </div>
    </div>
  );
}
```

### Pattern 4: Modal/Dropdown Menus

```typescript
function TransactionDropdownMenu({ transaction }) {
  return (
    <DropdownMenu>
      <DropdownMenuContent>
        {/* Edit link - doesn't need context preservation */}
        <DropdownMenuItem asChild>
          <Link href={`/transactions/${transaction.id}/edit`}>
            Edit Transaction
          </Link>
        </DropdownMenuItem>
        
        {/* View details - preserves context */}
        <DropdownMenuItem asChild>
          <TransactionLink transactionId={transaction.id}>
            View Details
          </TransactionLink>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
```

## Advanced Features

### Context Expiry

Navigation context automatically expires after 30 minutes to prevent stale navigation states.

### Browser Integration

The system works with browser back/forward buttons:
- If no navigation context is available, falls back to browser history
- If browser history is empty, navigates to logical fallback page

### Source Page Detection

The system automatically detects "source pages" that should be preserved:
- `/transactions` (with any filters/pagination)
- `/accounts` and `/accounts/[id]`
- `/categories` and `/categories/[id]`
- `/reports/*`

### URL Preservation

When preserving context, the system captures the complete URL including:
- Query parameters (filters, search terms)
- Pagination state
- Any other URL state

### Label Generation

Smart labels are generated for the back button:
- "Transactions" → "Back to Transactions"
- "Transactions?page=2" → "Back to Transactions (page 2)"
- "Transactions?account=123&page=2" → "Back to Transactions (filtered by account, page 2)"
- "/accounts/123" → "Back to Account Details"

## Migration Guide

### Updating Existing Transaction Links

Replace direct Next.js Links with contextual components:

```typescript
// Before
<Link href={`/transactions/${id}`}>View Transaction</Link>

// After
<TransactionLink transactionId={id}>View Transaction</TransactionLink>
```

### Updating Router Navigation

Replace router.push calls with context-aware navigation:

```typescript
// Before
const handleClick = () => {
  router.push(`/transactions/${id}`);
};

// After
const { navigateToTransaction } = useNavigationContext();
const handleClick = () => {
  navigateToTransaction(id);
};
```

### Updating Back Buttons

Replace static back buttons with smart ones:

```typescript
// Before
<Link href="/transactions">
  <Button>Back to Transactions</Button>
</Link>

// After
<TransactionBackButton />
```

## Browser Support

The system uses `sessionStorage` for context persistence, which is supported in all modern browsers. If `sessionStorage` is unavailable, the system gracefully falls back to standard navigation.

## Performance Considerations

- Navigation context is stored in session storage (small memory footprint)
- Context expires automatically to prevent memory leaks
- No network requests for navigation state management
- Breadcrumb generation is optimized for common patterns

## Accessibility

All components follow accessibility best practices:
- Proper ARIA labels for breadcrumbs
- Keyboard navigation support
- Screen reader compatible
- Focus management for back navigation

## Testing

The navigation system can be tested by:
1. Navigating from different source pages to transaction details
2. Verifying back navigation returns to correct context
3. Testing with different filter/pagination states
4. Ensuring fallback behavior works when context is unavailable