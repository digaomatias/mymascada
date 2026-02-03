# AI Suggestions Context Pattern

## Overview

This implementation solves the performance optimization challenge of duplicate AI API calls between batch categorization and individual CategoryPicker components using a **Context + Custom Hook Pattern**.

## Architecture

### Components

1. **AiSuggestionsContext**: Centralized state management for AI suggestions
2. **useAiSuggestions**: Core context hook with cache management
3. **useAiSuggestionsForTransaction**: Component-specific hook for individual transactions
4. **useAiSuggestionsBatch**: Hook for batch operations

### Key Features

- **Automatic Deduplication**: Prevents duplicate API calls for the same transaction
- **Smart Caching**: 5-minute TTL with automatic invalidation
- **Race Condition Prevention**: Request-in-flight tracking
- **Component Independence**: CategoryPicker works standalone or with shared cache
- **Type Safety**: Full TypeScript support with existing API types

## Usage

### 1. Wrap Pages with Provider

```tsx
import { AiSuggestionsProvider } from '@/contexts/ai-suggestions-context';

export default function CategorizePage() {
  return (
    <AiSuggestionsProvider>
      {/* Your page content */}
    </AiSuggestionsProvider>
  );
}
```

### 2. Use in Batch Components (LlmCategorizationSection)

```tsx
import { useAiSuggestionsBatch } from '@/hooks/use-ai-suggestions';

function LlmCategorizationSection() {
  const { storeBatchResults } = useAiSuggestionsBatch();
  
  const handleBatchCategorize = async () => {
    const response = await apiClient.batchCategorizeTransactions(request);
    
    // Store results in shared context
    storeBatchResults(response.categorizations);
    
    setSuggestions(response.categorizations);
  };
}
```

### 3. Use in Individual Components (CategoryPicker)

```tsx
import { useAiSuggestionsForTransaction } from '@/hooks/use-ai-suggestions';

function CategoryPicker({ transaction, ...props }) {
  const {
    suggestions,
    loading,
    topSuggestion,
    fetchSuggestions,
    applySuggestion,
    rejectSuggestion,
  } = useAiSuggestionsForTransaction({
    transactionId: transaction?.id || 0,
    transactionData: {
      description: transaction.description,
      amount: transaction.amount,
    },
    autoFetch: false, // Manual fetch on picker open
    confidenceThreshold: 0.85,
  });
  
  // Component automatically uses cached suggestions if available
  // Falls back to API call only if no cached data exists
}
```

## Data Flow

1. **User clicks "Categorize Selected"** → LlmCategorizationSection makes batch API call
2. **Batch results stored in context** → Available to all CategoryPicker components
3. **User opens CategoryPicker** → Checks context cache first
4. **Cache hit**: Uses cached suggestions immediately
5. **Cache miss**: Makes individual API call and caches result
6. **Cache invalidation**: When suggestion applied/rejected or TTL expires

## Performance Benefits

- **Eliminates Duplicate API Calls**: Same transaction never fetched twice within TTL
- **Faster User Experience**: Cached suggestions appear instantly
- **Reduced API Quota Usage**: Significant reduction in LLM API calls
- **Consistent Results**: Same AI suggestions across all components
- **Automatic Memory Management**: Context cleans up expired cache entries

## Cache Management

- **TTL**: 5 minutes per transaction
- **Invalidation**: Automatic on suggestion apply/reject
- **Memory**: Efficient Map-based storage with cleanup
- **Race Conditions**: Prevented with request-in-flight tracking

## Error Handling

- **Graceful Degradation**: Components work without AI suggestions
- **Silent Failures**: Network errors don't break UI
- **Retry Logic**: Users can manually retry failed requests
- **Consistent State**: Context maintains consistency across components

## Future Enhancements

- **Persistent Cache**: Store suggestions in localStorage
- **Background Refresh**: Pre-fetch suggestions for visible transactions
- **Analytics**: Track cache hit rates and API usage
- **Optimization**: Implement suggestion pre-loading strategies