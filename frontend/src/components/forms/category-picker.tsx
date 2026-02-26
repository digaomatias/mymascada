'use client';

import React, { useState, useMemo, useRef } from 'react';
import { Input } from '@/components/ui/input';
import { BaseModal } from '@/components/modals/base-modal';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { useAiSuggestionsForTransaction, useAiSuggestionInvalidation } from '@/hooks/use-ai-suggestions';
import { AiSuggestion } from '@/contexts/ai-suggestions-context';
import { useTranslations } from 'next-intl';
import { 
  MagnifyingGlassIcon,
  FolderIcon,
  ClockIcon,
  SparklesIcon,
  ShoppingBagIcon,
  TruckIcon,
  HeartIcon,
  BanknotesIcon,
  ReceiptPercentIcon,
  CheckIcon,
  ChevronDownIcon,
  XMarkIcon,
  CheckCircleIcon,
  LightBulbIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';

// Quick pick categories with icons - these should map to common category patterns
// Updated to better match actual category names in the system
// Note: 'name' is used for category matching logic and must stay in English.
// 'labelKey' is used for display and maps to categories.quickPicks.* translation keys.
const QUICK_PICKS = [
  { id: 'food_dining', canonicalKey: 'food_dining', name: 'Food & Dining', labelKey: 'food' as const, keywords: ['food', 'dining', 'restaurant', 'grocery', 'alimentação', 'supermercado'], icon: ShoppingBagIcon, color: 'bg-orange-100 text-orange-700' },
  { id: 'transportation', canonicalKey: 'transportation', name: 'Transportation', labelKey: 'transport' as const, keywords: ['transport', 'travel', 'gas', 'fuel', 'uber', 'taxi', 'transporte', 'combustível'], icon: TruckIcon, color: 'bg-blue-100 text-blue-700' },
  { id: 'personal_care', canonicalKey: 'personal_care', name: 'Clothing', labelKey: 'clothing' as const, keywords: ['clothing', 'apparel', 'fashion', 'clothes', 'roupa', 'vestuário'], icon: ShoppingBagIcon, color: 'bg-pink-100 text-pink-700' },
  { id: 'housing_utilities', canonicalKey: 'housing_utilities', name: 'Housing & Utilities', labelKey: 'utilities' as const, keywords: ['housing', 'utilities', 'rent', 'mortgage', 'electric', 'water', 'moradia', 'serviços'], icon: ReceiptPercentIcon, color: 'bg-red-100 text-red-700' },
  { id: 'health_medical', canonicalKey: 'health_medical', name: 'Health & Medical', labelKey: 'health' as const, keywords: ['health', 'medical', 'doctor', 'pharmacy', 'hospital', 'healthcare', 'saúde', 'médico'], icon: HeartIcon, color: 'bg-green-100 text-green-700' },
  { id: 'income', canonicalKey: 'income', name: 'Income', labelKey: 'income' as const, keywords: ['income', 'salary', 'wage', 'bonus', 'freelance', 'receita', 'salário'], icon: BanknotesIcon, color: 'bg-emerald-100 text-emerald-700' },
];

interface Category {
  id: number;
  name: string;
  canonicalKey?: string;
  type: number;
  parentId: number | null;
  color?: string;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}

// Helper function to get suggestion source label key based on categorization method
const getAiSuggestionSourceKey = (suggestion: AiSuggestion): string => {
  const method = suggestion.method || (suggestion as any).categorization_method || 'LLM';
  switch (method) {
    case 'Rules':
    case 'Rule':
      return 'ruleSource';
    case 'ML':
      return 'mlSource';
    case 'LLM':
      return 'aiSource';
    case 'Manual':
      return 'manualSource';
    default:
      return 'aiSource';
  }
};

// Helper function to sort suggestions with rule-based suggestions prioritized first
const sortSuggestionsByPriority = (suggestions: AiSuggestion[]): AiSuggestion[] => {
  return [...suggestions].sort((a, b) => {
    const aMethod = a.method || (a as any).categorization_method || 'LLM';
    const bMethod = b.method || (b as any).categorization_method || 'LLM';
    
    // Define priority order: Rules > ML > LLM > others
    const methodPriority = {
      'Rules': 1,
      'Rule': 1,
      'ML': 2,
      'LLM': 3,
      'Manual': 4
    };
    
    const aPriority = methodPriority[aMethod as keyof typeof methodPriority] || 5;
    const bPriority = methodPriority[bMethod as keyof typeof methodPriority] || 5;
    
    // First sort by method priority (lower number = higher priority)
    if (aPriority !== bPriority) {
      return aPriority - bPriority;
    }
    
    // Within same method, sort by confidence (higher confidence first)
    return b.confidence - a.confidence;
  });
};



interface CategoryPickerProps {
  value?: string | number;
  onChange: (categoryId: string | number) => void;
  categories: Category[];
  recentCategories?: number[];
  placeholder?: string;
  error?: boolean;
  disabled?: boolean;
  disableQuickPicks?: boolean;
  // Enhanced AI features
  aiSuggestions?: AiSuggestion[];
  isLoadingAiSuggestions?: boolean;
  onAiSuggestionApplied?: (suggestion: AiSuggestion) => void;
  onAiSuggestionRejected?: (suggestion: AiSuggestion) => void;
  showAiSuggestions?: boolean;
  autoApplyHighConfidence?: boolean;
  confidenceThreshold?: number;
  // Flag to indicate that AI suggestions are being provided externally (batch mode)
  useExternalAiSuggestions?: boolean;
  transaction?: {
    id: number;
    description: string;
    amount: number;
  };
}

export function CategoryPicker({
  value,
  onChange,
  categories,
  recentCategories = [],
  placeholder = 'Select a category',
  error = false,
  disabled = false,
  disableQuickPicks = false,
  // AI features
  aiSuggestions = [],
  onAiSuggestionApplied,
  onAiSuggestionRejected,
  showAiSuggestions = true,
  autoApplyHighConfidence = false,
  confidenceThreshold = 0.85,
  useExternalAiSuggestions = false,
  transaction
}: CategoryPickerProps) {
  const t = useTranslations('categories');
  const tCommon = useTranslations('common');
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const [rejectedSuggestions, setRejectedSuggestions] = useState<Set<number>>(new Set());
  const [appliedSuggestion, setAppliedSuggestion] = useState<AiSuggestion | null>(null);
  const { isMobile } = useDeviceDetect();
  const inputRef = useRef<HTMLInputElement>(null);
  const preventFocusRef = useRef(false);

  // Use the smart AI suggestions hook
  const aiSuggestionsFromHook = useAiSuggestionsForTransaction({
    transactionId: transaction?.id || 0,
    autoFetch: false, // We'll control when to fetch
    confidenceThreshold: 0.4
  });
  
  const { invalidateTransaction } = useAiSuggestionInvalidation();

  // Use provided AI suggestions first, then hook suggestions
  // If useExternalAiSuggestions is true, always use the provided aiSuggestions (even if empty)
  const effectiveAiSuggestions = useExternalAiSuggestions
    ? aiSuggestions 
    : (aiSuggestions.length > 0 ? aiSuggestions : aiSuggestionsFromHook.suggestions);

  // Loading state from hook
  const effectiveIsLoading = aiSuggestionsFromHook.isLoading;

  // Debug logging to trace data flow
  React.useEffect(() => {
    if (transaction && effectiveAiSuggestions.length > 0) {
      console.log(`[CategoryPicker Debug] Transaction ${transaction.id}:`, {
        description: transaction.description,
        suggestionsCount: effectiveAiSuggestions.length,
        suggestions: effectiveAiSuggestions.map(s => ({
          categoryId: s.categoryId,
          categoryName: s.categoryName,
          confidence: s.confidence,
          method: s.method,
          categorization_method: (s as any).categorization_method,
          raw_suggestion: s,
          reasoning: s.reasoning?.slice(0, 50) + '...'
        })),
        useExternalAiSuggestions,
        isLoading: effectiveIsLoading
      });
      
      // Also check the source label for first suggestion
      if (effectiveAiSuggestions.length > 0) {
        const firstSuggestion = effectiveAiSuggestions[0];
        console.log(`[CategoryPicker Debug] First suggestion source label:`, {
          suggestion: firstSuggestion,
          sourceLabel: getAiSuggestionSourceKey(firstSuggestion),
          method: firstSuggestion.method,
          categorization_method: (firstSuggestion as any).categorization_method
        });
      }
    }
  }, [transaction, effectiveAiSuggestions, useExternalAiSuggestions, effectiveIsLoading]);



  // Get selected category
  const selectedCategory = (categories || []).find(cat => cat?.id === Number(value));

  // Filter AI suggestions based on rejected ones and current selection
  const activeAiSuggestions = showAiSuggestions 
    ? effectiveAiSuggestions.filter(suggestion => 
        !rejectedSuggestions.has(suggestion.categoryId) && 
        suggestion.categoryId !== Number(value)
      )
    : [];

  // Get the top AI suggestion for display (prioritize rules first, then by confidence)
  const topAiSuggestion = activeAiSuggestions.length > 0 
    ? sortSuggestionsByPriority(activeAiSuggestions)[0]
    : null;

  // Check if we should auto-apply high confidence suggestions
  const shouldAutoApply = autoApplyHighConfidence && 
    topAiSuggestion && 
    topAiSuggestion.confidence >= confidenceThreshold &&
    !value;

  // Auto-apply high confidence suggestion
  React.useEffect(() => {
    if (shouldAutoApply && topAiSuggestion && !appliedSuggestion) {
      setAppliedSuggestion(topAiSuggestion);
      onChange(topAiSuggestion.categoryId);
      onAiSuggestionApplied?.(topAiSuggestion);
    }
  }, [shouldAutoApply, topAiSuggestion, appliedSuggestion, onChange, onAiSuggestionApplied]);


  // Get recent category objects from all categories (not filtered)
  const recentCategoryObjects = (recentCategories || [])
    .map(id => (categories || []).find(cat => cat?.id === id))
    .filter(Boolean) as Category[];


  const handleSelect = (categoryId: number, isAiSuggestion = false, suggestion?: AiSuggestion) => {
    onChange(categoryId);
    setIsOpen(false);
    setSearchTerm('');
    setSelectedIndex(-1);
    
    // Handle AI suggestion tracking
    if (isAiSuggestion && suggestion) {
      setAppliedSuggestion(suggestion);
      onAiSuggestionApplied?.(suggestion);
      
      // Invalidate cache for this transaction if using hook suggestions
      if (transaction && aiSuggestions.length === 0) {
        invalidateTransaction(transaction.id);
      }
    }
    
    // Prevent focus from reopening on desktop after selection
    if (!isMobile) {
      preventFocusRef.current = true;
      setTimeout(() => {
        preventFocusRef.current = false;
      }, 100);
    }
  };

  const handleRejectAiSuggestion = (suggestion: AiSuggestion) => {
    setRejectedSuggestions(prev => new Set([...prev, suggestion.categoryId]));
    onAiSuggestionRejected?.(suggestion);
    
    // Invalidate cache for this transaction if using hook suggestions
    if (transaction && aiSuggestions.length === 0) {
      invalidateTransaction(transaction.id);
    }
  };

  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.8) return 'text-green-600 bg-green-50 border-green-200';
    if (confidence >= 0.6) return 'text-yellow-600 bg-yellow-50 border-yellow-200';
    return 'text-orange-600 bg-orange-50 border-orange-200';
  };

  const getConfidenceIcon = (confidence: number) => {
    if (confidence >= 0.8) return CheckCircleIcon;
    if (confidence >= 0.6) return LightBulbIcon;
    return ExclamationTriangleIcon;
  };

  const getConfidenceText = (confidence: number) => {
    if (confidence >= 0.8) return 'High';
    if (confidence >= 0.6) return 'Medium';
    return 'Low';
  };

  const getMethodColors = (method?: string) => {
    switch (method) {
      case 'Rule':
        return {
          button: 'bg-blue-600 hover:bg-blue-700',
          text: 'Rule-based',
          bgColor: 'bg-blue-50',
          textColor: 'text-blue-700'
        };
      case 'ML':
        return {
          button: 'bg-green-600 hover:bg-green-700',
          text: 'ML prediction',
          bgColor: 'bg-green-50',
          textColor: 'text-green-700'
        };
      case 'LLM':
        return {
          button: 'bg-purple-600 hover:bg-purple-700',
          text: 'AI analysis',
          bgColor: 'bg-purple-50',
          textColor: 'text-purple-700'
        };
      default:
        return {
          button: 'bg-gray-600 hover:bg-gray-700',
          text: 'Suggestion',
          bgColor: 'bg-gray-50',
          textColor: 'text-gray-700'
        };
    }
  };

  // Desktop: Use focus events for keyboard accessibility
  const handleDesktopFocus = () => {
    if (!preventFocusRef.current) {
      setIsOpen(true);
      setSearchTerm('');
      
      
      // Set initial selection based on current value for quick picks
      if (!disableQuickPicks) {
        const currentQuickPickIndex = QUICK_PICKS.findIndex(pick => {
          // Use same matching logic as handleQuickPick
          let matchingCategory = (categories || []).find(cat =>
            cat.canonicalKey && cat.canonicalKey === pick.canonicalKey
          );
          if (!matchingCategory) {
            matchingCategory = (categories || []).find(cat => {
              const catNameLower = cat?.name?.toLowerCase?.();
              return catNameLower && pick.keywords.some(keyword =>
                catNameLower.includes(keyword.toLowerCase())
              );
            });
          }
          return matchingCategory && matchingCategory.id === Number(value);
        });
        setSelectedIndex(currentQuickPickIndex >= 0 ? currentQuickPickIndex : 0);
      }
    }
  };

  const handleDesktopBlur = (e: React.FocusEvent) => {
    // Check if focus is moving within the component
    if (!e.currentTarget.contains(e.relatedTarget as Node)) {
      // Delay closing to allow for clicks
      setTimeout(() => {
        setIsOpen(false);
        setSelectedIndex(-1);
      }, 150);
    }
  };

  // Mobile: Use click events only
  const handleMobileClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (disabled) return;
    setIsOpen(true);
    
    
    // Blur the input to hide any potential keyboard
    inputRef.current?.blur();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    // If closed and Enter is pressed, reopen
    if (!isOpen && e.key === 'Enter') {
      e.preventDefault();
      setIsOpen(true);
      setSearchTerm('');
      
      // Set initial selection based on current value
      if (!searchTerm && !disableQuickPicks) {
        const currentQuickPickIndex = QUICK_PICKS.findIndex(pick => {
          // Use same matching logic as handleQuickPick
          let matchingCategory = (categories || []).find(cat =>
            cat.canonicalKey && cat.canonicalKey === pick.canonicalKey
          );
          if (!matchingCategory) {
            matchingCategory = (categories || []).find(cat => {
              const catNameLower = cat?.name?.toLowerCase?.();
              return catNameLower && pick.keywords.some(keyword =>
                catNameLower.includes(keyword.toLowerCase())
              );
            });
          }
          return matchingCategory && matchingCategory.id === Number(value);
        });
        setSelectedIndex(currentQuickPickIndex >= 0 ? currentQuickPickIndex : 0);
      } else {
        setSelectedIndex(-1);
      }
      return;
    }

    if (!isOpen) return;

    // Get available options for navigation
    const availableOptions = searchTerm ? searchResults : (disableQuickPicks ? [] : QUICK_PICKS);
    if (availableOptions.length === 0) return;

    // Initialize selection if not set
    if (selectedIndex === -1) {
      if (searchTerm) {
        setSelectedIndex(0);
        return;
      } else {
        // For quick picks, try to start from currently selected category
        if (!disableQuickPicks) {
          const currentQuickPickIndex = QUICK_PICKS.findIndex(pick => {
            // Use same matching logic as handleQuickPick
            let matchingCategory = (categories || []).find(cat =>
              cat.canonicalKey && cat.canonicalKey === pick.canonicalKey
            );
            if (!matchingCategory) {
              matchingCategory = (categories || []).find(cat => {
                const catNameLower = cat?.name?.toLowerCase?.();
                return catNameLower && pick.keywords.some(keyword =>
                  catNameLower.includes(keyword.toLowerCase())
                );
              });
            }
            return matchingCategory && matchingCategory.id === Number(value);
          });
          setSelectedIndex(currentQuickPickIndex >= 0 ? currentQuickPickIndex : 0);
        }
        return;
      }
    }

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (searchTerm) {
          // Linear navigation for search results
          setSelectedIndex(prev => 
            prev < availableOptions.length - 1 ? prev + 1 : 0
          );
        } else {
          // 2D navigation for quick picks (3 columns)
          const cols = 3;
          setSelectedIndex(prev => {
            const newIndex = prev + cols;
            return newIndex < availableOptions.length ? newIndex : prev;
          });
        }
        break;
      case 'ArrowUp':
        e.preventDefault();
        if (searchTerm) {
          // Linear navigation for search results
          setSelectedIndex(prev => 
            prev > 0 ? prev - 1 : availableOptions.length - 1
          );
        } else {
          // 2D navigation for quick picks (3 columns)
          const cols = 3;
          setSelectedIndex(prev => {
            const newIndex = prev - cols;
            return newIndex >= 0 ? newIndex : prev;
          });
        }
        break;
      case 'ArrowLeft':
        if (!searchTerm) {
          e.preventDefault();
          // 2D navigation for quick picks only
          setSelectedIndex(prev => {
            if (prev <= 0) return availableOptions.length - 1; // Wrap to end
            return prev - 1;
          });
        }
        break;
      case 'ArrowRight':
        if (!searchTerm) {
          e.preventDefault();
          // 2D navigation for quick picks only
          setSelectedIndex(prev => {
            if (prev >= availableOptions.length - 1) return 0; // Wrap to start
            return prev + 1;
          });
        }
        break;
      case 'Enter':
        e.preventDefault();
        if (selectedIndex >= 0 && selectedIndex < availableOptions.length) {
          if (searchTerm) {
            // Selecting from search results
            const selectedItem = availableOptions[selectedIndex] as Category;
            handleSelect(selectedItem.id);
          } else {
            // Selecting from quick picks
            const selectedItem = availableOptions[selectedIndex] as typeof QUICK_PICKS[0];
            handleQuickPick(selectedItem.id);
          }
        }
        break;
      case 'Escape':
        e.preventDefault();
        setIsOpen(false);
        setSearchTerm('');
        setSelectedIndex(-1);
        break;
    }
  };

  const handleQuickPick = (quickPickId: string) => {
    const quickPick = QUICK_PICKS.find(pick => pick.id === quickPickId);
    if (!quickPick) return;

    // 1. Primary: Match by canonicalKey (works regardless of display language)
    let matchingCategory = (categories || []).find(cat =>
      cat.canonicalKey && cat.canonicalKey === quickPick.canonicalKey
    );

    // 2. Fallback: keyword matching (for categories without canonicalKey)
    if (!matchingCategory) {
      matchingCategory = (categories || []).find(cat => {
        const catNameLower = cat?.name?.toLowerCase?.();
        return catNameLower && quickPick.keywords.some(keyword =>
          catNameLower.includes(keyword.toLowerCase())
        );
      });
    }

    if (matchingCategory) {
      handleSelect(matchingCategory.id);
    } else {
      console.warn(`No category found for quick pick: ${quickPick.canonicalKey}`, {
        availableCategories: categories.map(c => ({ name: c.name, canonicalKey: c.canonicalKey })),
        searchedKeywords: quickPick.keywords
      });
    }
  };

  // Note: Category tree rendering is not currently used in this implementation

  // Get search results (limit to 3 for compactness)
  const searchResults = useMemo(() => {
    if (!searchTerm) return [];
    
    // When searching, always use all categories to ensure we find matches
    const searchCategories = (categories || []);
    if (!Array.isArray(searchCategories) || searchCategories.length === 0) return [];
    
    const searchLower = searchTerm.toLowerCase();
    return searchCategories
      .filter(cat => cat?.name?.toLowerCase?.()?.includes(searchLower))
      .slice(0, 10); // Show more results when searching
  }, [categories, searchTerm]);

  // Check if there's content to show in the dropdown
  // Always show when searching (even if no results), or when there are quick picks/recent categories
  const hasContent = Boolean(searchTerm) || !disableQuickPicks || recentCategoryObjects.length > 0;

  const pickerContent = (
    <div className="space-y-4">
      {/* AI Suggestions - Show button first, then suggestions */}
      {!searchTerm && showAiSuggestions && transaction && (
        <div>
          {/* Show button if no suggestions yet */}
          {activeAiSuggestions.length === 0 && !effectiveIsLoading && (
            <button
              type="button"
              onClick={(e) => {
                e.preventDefault();
                e.stopPropagation();
                aiSuggestionsFromHook.fetchSuggestions();
              }}
              onMouseDown={(e) => {
                // Prevent blur from firing
                e.preventDefault();
              }}
              className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-gradient-to-r from-purple-500 to-blue-500 hover:from-purple-600 hover:to-blue-600 text-white rounded-lg transition-all duration-200 shadow-sm hover:shadow-md cursor-pointer"
            >
              <SparklesIcon className="w-5 h-5" />
              <span className="font-medium">{t('aiSuggestions.getButton')}</span>
            </button>
          )}
          
          {/* Show suggestions when available */}
          {(activeAiSuggestions.length > 0 || effectiveIsLoading) && (
            <div>
              <div className="flex items-center gap-2 mb-3 text-sm font-medium text-purple-700">
                <SparklesIcon className={`w-4 h-4 ${effectiveIsLoading ? 'animate-pulse' : ''}`} />
                {t('aiSuggestions.title')}
                {effectiveIsLoading && (
                  <span className="text-xs text-gray-500 font-normal animate-pulse">
                    {t('aiSuggestions.analyzing')}
                  </span>
                )}
                {!effectiveIsLoading && transaction && (
                  <span className="text-xs text-gray-500 font-normal">
                    {t('aiSuggestions.forTransaction', { description: transaction.description.slice(0, 30) })}
                  </span>
                )}
              </div>
          
          {/* Loading state */}
          {effectiveIsLoading && (
            <div className="space-y-2">
              {[...Array(2)].map((_, i) => (
                <div key={i} className="p-3 rounded-lg border-2 border-gray-200 bg-gray-50 animate-pulse">
                  <div className="flex items-center gap-2 mb-2">
                    <div className="w-4 h-4 bg-gray-300 rounded"></div>
                    <div className="h-4 bg-gray-300 rounded w-32"></div>
                    <div className="h-4 bg-gray-300 rounded w-16"></div>
                  </div>
                  <div className="h-3 bg-gray-300 rounded w-full mb-2"></div>
                  <div className="flex gap-2">
                    <div className="h-6 bg-gray-300 rounded w-16"></div>
                    <div className="h-6 bg-gray-300 rounded w-12"></div>
                  </div>
                </div>
              ))}
            </div>
          )}
          {/* AI suggestion cards */}
          {!effectiveIsLoading && (
            <div className="space-y-2">
              {sortSuggestionsByPriority(activeAiSuggestions).slice(0, 3).map((suggestion, index) => {
              const confidenceColor = getConfidenceColor(suggestion.confidence);
              const confidencePercent = Math.round(suggestion.confidence * 100);
              
              return (
                <div 
                  key={`${suggestion.categoryId}-${suggestion.method || 'unknown'}-${index}`}
                  className={`
                    relative p-3 rounded-lg border-2 transition-all duration-200
                    ${confidenceColor}
                    ${index === 0 ? 'ring-2 ring-purple-200 shadow-md' : 'hover:shadow-sm'}
                  `}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        {React.createElement(getConfidenceIcon(suggestion.confidence), {
                          className: `w-5 h-5 ${
                            suggestion.confidence >= 0.8 ? 'text-green-600' :
                            suggestion.confidence >= 0.6 ? 'text-yellow-600' : 'text-orange-600'
                          }`
                        })}
                        <span className="font-medium text-gray-900">
                          {suggestion.categoryName}
                        </span>
                        {suggestion.method && (
                          <span className={`
                            px-2 py-1 rounded-full text-xs font-medium
                            ${getMethodColors(suggestion.method).bgColor} ${getMethodColors(suggestion.method).textColor}
                          `}>
                            {getMethodColors(suggestion.method).text}
                          </span>
                        )}
                        <span className={`
                          px-2 py-1 rounded-full text-xs font-medium
                          ${confidenceColor}
                        `}>
                          {getConfidenceText(suggestion.confidence)} {confidencePercent}%
                        </span>
                      </div>
                      {suggestion.reasoning && (
                        <p className="text-xs text-gray-600 mb-2 line-clamp-2">
                          {suggestion.reasoning}
                        </p>
                      )}
                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleSelect(suggestion.categoryId, true, suggestion);
                          }}
                          className={`
                            flex items-center gap-1 px-3 py-1 text-white 
                            rounded-md text-xs font-medium transition-colors cursor-pointer
                            ${getMethodColors(suggestion.method).button}
                          `}
                          title={t('aiSuggestions.applyWithMethod', { method: getMethodColors(suggestion.method).text })}
                        >
                          <CheckIcon className="w-3 h-3" />
                          {tCommon('apply')}
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleRejectAiSuggestion(suggestion);
                          }}
                          className="
                            flex items-center gap-1 px-3 py-1 bg-gray-100 text-gray-600 
                            rounded-md text-xs font-medium hover:bg-gray-200 transition-colors cursor-pointer
                          "
                        >
                          <XMarkIcon className="w-3 h-3" />
                          {t('aiSuggestions.skip')}
                        </button>
                      </div>
                    </div>
                  </div>
                  {index === 0 && suggestion.confidence >= 0.8 && (
                    <div className="absolute -top-1 -right-1">
                      <div className="bg-green-500 text-white text-xs px-2 py-1 rounded-full font-medium">
                        {t('aiSuggestions.recommended')}
                      </div>
                    </div>
                  )}
                </div>
              );
              })}
            </div>
          )}
            </div>
          )}
        </div>
      )}

      {/* Search Results */}
      {searchTerm && (
        <div>
          {searchResults.length > 0 ? (
            <div className="space-y-1">
              {searchResults.map((cat, index) => (
                <button
                  key={cat.id}
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleSelect(cat.id);
                  }}
                  className={`
                    w-full flex items-center gap-3 px-3 py-2 rounded-md text-left transition-colors
                    ${index === selectedIndex ? 'bg-primary-50 ring-2 ring-primary-200' : 
                      cat.id === Number(value) ? 'bg-primary-100 text-primary-700' : 'hover:bg-gray-50'}
                  `}
                >
                  <FolderIcon className="w-4 h-4 text-gray-400" />
                  <span className="text-sm">{cat.name}</span>
                  {cat.id === Number(value) && (
                    <CheckIcon className="w-4 h-4 text-primary-600 ml-auto" />
                  )}
                </button>
              ))}
            </div>
          ) : (
            <p className="text-center text-gray-500 py-4 text-sm">
              {t('noCategoriesFoundFor', { searchTerm })}
            </p>
          )}
        </div>
      )}


      {/* Quick Picks - Only show when not searching and not disabled */}
      {!searchTerm && !disableQuickPicks && (
        <div>
          <div className="flex items-center gap-2 mb-3 text-sm font-medium text-gray-700">
            <SparklesIcon className="w-4 h-4" />
            {t('quickPicksTitle')}
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
            {QUICK_PICKS.map((pick, index) => (
              <button
                key={pick.id}
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  handleQuickPick(pick.id);
                }}
                className={`
                  flex items-center gap-2 px-3 py-2 rounded-lg border transition-colors
                  ${!searchTerm && index === selectedIndex ? 'ring-2 ring-primary-400 ring-offset-1' : ''}
                  ${pick.color} border-current/20 hover:scale-105
                `}
              >
                <pick.icon className="w-4 h-4" />
                <span className="text-xs font-medium">{t(`quickPicks.${pick.labelKey}`)}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Recent Categories - Only show when not searching */}
      {!searchTerm && recentCategoryObjects.length > 0 && (
        <div>
          <div className="flex items-center gap-2 mb-3 text-sm font-medium text-gray-700">
            <ClockIcon className="w-4 h-4" />
            {t('recentlyUsedTitle')}
          </div>
          <div className="space-y-1">
            {recentCategoryObjects.map(cat => (
              <button
                key={cat.id}
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  handleSelect(cat.id);
                }}
                className={`
                  w-full flex items-center gap-3 px-3 py-2 rounded-md text-left transition-colors
                  ${cat.id === Number(value) ? 'bg-primary-100 text-primary-700' : 'hover:bg-gray-50'}
                `}
              >
                <FolderIcon className="w-4 h-4 text-gray-400" />
                <span className="text-sm">{cat.name}</span>
                {cat.id === Number(value) && (
                  <CheckIcon className="w-4 h-4 text-primary-600 ml-auto" />
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );

  // Desktop: Inline dropdown replacement
  if (!isMobile) {
    return (
      <div className="relative">
        <div className="relative">
          {/* AI indicator when there are suggestions */}
          {!isOpen && topAiSuggestion && !selectedCategory && (
            <div className="absolute left-3 top-1/2 transform -translate-y-1/2 z-10">
              <div className="flex items-center gap-1 text-purple-600 animate-pulse">
                <SparklesIcon className="w-4 h-4" />
                <span className="text-xs font-medium">{t('aiBadge')}</span>
              </div>
            </div>
          )}
          {isOpen && (
            <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
          )}
          <Input
            type="text"
            placeholder={
              selectedCategory ? selectedCategory.name : 
              topAiSuggestion ? `${t(getAiSuggestionSourceKey(topAiSuggestion))}: ${topAiSuggestion.categoryName}` :
              placeholder
            }
            value={isOpen ? searchTerm : (selectedCategory ? selectedCategory.name : '')}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setSelectedIndex(-1); // Reset selection when typing
            }}
            onKeyDown={handleKeyDown}
            onFocus={handleDesktopFocus}
            onBlur={handleDesktopBlur}
            disabled={disabled}
            className={`
              ${isOpen ? 'pl-10' : topAiSuggestion && !selectedCategory ? 'pl-16' : 'pl-4'} 
              pr-10 
              ${error ? 'border-red-300' : ''}
              ${topAiSuggestion && !selectedCategory ? 'border-purple-300 bg-purple-50' : ''}
            `}
          />
          {/* AI quick apply button */}
          {!isOpen && topAiSuggestion && !selectedCategory && (
            <button
              type="button"
              onClick={(e) => {
                e.preventDefault();
                e.stopPropagation();
                handleSelect(topAiSuggestion.categoryId, true, topAiSuggestion);
              }}
              className="
                absolute right-8 top-1/2 transform -translate-y-1/2 
                px-2 py-1 bg-purple-600 text-white text-xs rounded 
                hover:bg-purple-700 transition-colors cursor-pointer
              "
            >
              {tCommon('apply')}
            </button>
          )}
          <ChevronDownIcon 
            className="absolute right-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400 cursor-pointer" 
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              if (!disabled) {
                setIsOpen(!isOpen);
                if (!isOpen) {
                  setSearchTerm('');
                  setSelectedIndex(-1);
                }
              }
            }}
          />
        </div>

        {isOpen && !disabled && hasContent && (
          <div className="absolute z-[9999] mt-1 w-full min-w-80 bg-white rounded-lg shadow-lg border border-gray-200 p-4 left-0">
            {pickerContent}
          </div>
        )}
      </div>
    );
  }

  // Mobile: Modal approach
  return (
    <>
      <div className="relative">
        {/* AI indicator for mobile */}
        {topAiSuggestion && !selectedCategory ? (
          <div className="absolute left-3 top-1/2 transform -translate-y-1/2 z-10">
            <div className="flex items-center gap-1 text-purple-600 animate-pulse">
              <SparklesIcon className="w-4 h-4" />
              <span className="text-xs font-medium">{t('aiBadge')}</span>
            </div>
          </div>
        ) : (
          <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
        )}
        <Input
          ref={inputRef}
          type="text"
          placeholder={
            selectedCategory ? selectedCategory.name : 
            topAiSuggestion ? `${t(getAiSuggestionSourceKey(topAiSuggestion))}: ${topAiSuggestion.categoryName}` :
            placeholder
          }
          value={selectedCategory ? selectedCategory.name : ''}
          onClick={handleMobileClick}
          readOnly
          disabled={disabled}
          inputMode="none"
          className={`
            ${topAiSuggestion && !selectedCategory ? 'pl-16' : 'pl-10'} 
            pr-10 cursor-pointer 
            ${error ? 'border-red-300' : ''}
            ${topAiSuggestion && !selectedCategory ? 'border-purple-300 bg-purple-50' : ''}
          `}
        />
        <ChevronDownIcon 
          className="absolute right-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400 cursor-pointer" 
          onClick={handleMobileClick}
        />
      </div>

      <BaseModal
        isOpen={isOpen && !disabled}
        onClose={() => {
          setIsOpen(false);
          setSearchTerm('');
        }}
        title={t('selectCategory')}
        size="lg"
      >
        <div className="space-y-4">
          {/* Search Bar in Modal */}
          <div className="relative">
            <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              type="text"
              placeholder={t('searchCategories')}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10"
              autoFocus
            />
          </div>
          {hasContent ? pickerContent : (
            <div className="text-center py-8 text-gray-500">
              {t('noCategoriesAvailable')}
            </div>
          )}
        </div>
      </BaseModal>
    </>
  );
}
