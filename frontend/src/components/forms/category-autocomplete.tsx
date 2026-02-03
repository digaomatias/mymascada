'use client';

import { useState, useEffect, useRef, KeyboardEvent, useCallback } from 'react';
import { Input } from '@/components/ui/input';
import { 
  CheckIcon, 
  TagIcon,
  PlusIcon,
  ChevronRightIcon
} from '@heroicons/react/24/outline';

interface Category {
  id: number;
  name: string;
  color?: string;
  type?: number;
  isSystemCategory?: boolean;
  fullPath?: string;
  parentCategoryId?: number;
}

interface CategorySuggestion {
  id?: number;
  name: string;
  fullPath: string;
  isExisting: boolean;
  parentPath?: string;
  needsCreation: boolean;
}

interface CategoryAutocompleteProps {
  categories: Category[];
  value: string;
  onChange: (categoryId: string) => void;
  onCreateCategory: (categoryPath: string) => Promise<Category | null>;
  placeholder?: string;
  disabled?: boolean;
  error?: string;
}

export default function CategoryAutocomplete({
  categories,
  value,
  onChange,
  onCreateCategory,
  placeholder = "Type to search or create categories...",
  disabled = false,
  error
}: CategoryAutocompleteProps) {
  const [inputValue, setInputValue] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [suggestions, setSuggestions] = useState<CategorySuggestion[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const [isCreating, setIsCreating] = useState(false);
  
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSelectedIndex(-1);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Build category hierarchy map
  const categoryMap = new Map<string, Category>();
  const hierarchyMap = new Map<string, string[]>();
  
  categories.forEach(cat => {
    const path = cat.fullPath || cat.name;
    categoryMap.set(path, cat);
    
    // Build hierarchy relationships
    const parts = path.split(' -> ');
    for (let i = 0; i < parts.length; i++) {
      const currentPath = parts.slice(0, i + 1).join(' -> ');
      if (!hierarchyMap.has(currentPath)) {
        hierarchyMap.set(currentPath, []);
      }
      
      // Add children
      if (i < parts.length - 1) {
        const childPath = parts.slice(0, i + 2).join(' -> ');
        if (!hierarchyMap.get(currentPath)?.includes(childPath)) {
          hierarchyMap.get(currentPath)?.push(childPath);
        }
      }
    }
  });

  // Generate suggestions based on input
  const generateSuggestions = useCallback((input: string): CategorySuggestion[] => {
    if (!input.trim()) return [];

    const results: CategorySuggestion[] = [];
    const lowerInput = input.toLowerCase();

    // Check if input contains hierarchy separator
    const hasHierarchy = input.includes(' -> ');
    
    if (hasHierarchy) {
      // Handle hierarchical input
      const parts = input.split(' -> ').map(p => p.trim());
      const partialPath = parts.join(' -> ');
      
      // Look for exact matches in existing categories
      const exactMatches = categories.filter(cat => 
        (cat.fullPath || cat.name).toLowerCase().includes(lowerInput)
      );
      
      exactMatches.forEach(cat => {
        results.push({
          id: cat.id,
          name: cat.name,
          fullPath: cat.fullPath || cat.name,
          isExisting: true,
          needsCreation: false
        });
      });
      
      // If no exact match, suggest creating the hierarchy
      if (exactMatches.length === 0) {
        results.push({
          name: parts[parts.length - 1],
          fullPath: partialPath,
          isExisting: false,
          needsCreation: true,
          parentPath: parts.length > 1 ? parts.slice(0, -1).join(' -> ') : undefined
        });
      }
    } else {
      // Handle simple input - search all categories
      
      // Exact starts-with matches first
      const startsWithMatches = categories.filter(cat =>
        cat.name.toLowerCase().startsWith(lowerInput)
      );
      
      // Contains matches second
      const containsMatches = categories.filter(cat =>
        cat.name.toLowerCase().includes(lowerInput) && 
        !cat.name.toLowerCase().startsWith(lowerInput)
      );
      
      // Hierarchy matches third
      const hierarchyMatches = categories.filter(cat =>
        (cat.fullPath || cat.name).toLowerCase().includes(lowerInput) &&
        !cat.name.toLowerCase().includes(lowerInput)
      );
      
      // Add all matches
      [...startsWithMatches, ...containsMatches, ...hierarchyMatches].forEach(cat => {
        results.push({
          id: cat.id,
          name: cat.name,
          fullPath: cat.fullPath || cat.name,
          isExisting: true,
          needsCreation: false
        });
      });
      
      // Add option to create new simple category
      if (input.trim() && !results.some(r => r.name.toLowerCase() === lowerInput)) {
        results.push({
          name: input.trim(),
          fullPath: input.trim(),
          isExisting: false,
          needsCreation: true
        });
      }
    }

    // Remove duplicates
    const uniqueResults = results.filter((item, index) => 
      results.findIndex(r => r.fullPath === item.fullPath) === index
    );

    return uniqueResults.slice(0, 8); // Limit to 8 suggestions
  }, [categories]);

  useEffect(() => {
    const newSuggestions = generateSuggestions(inputValue);
    setSuggestions(newSuggestions);
    setSelectedIndex(-1);
  }, [inputValue, categories, generateSuggestions]);

  const handleInputChange = (value: string) => {
    setInputValue(value);
    setIsOpen(true);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen && e.key !== 'ArrowDown') return;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (!isOpen) {
          setIsOpen(true);
        } else {
          setSelectedIndex(prev => 
            prev < suggestions.length - 1 ? prev + 1 : 0
          );
        }
        break;
        
      case 'ArrowUp':
        e.preventDefault();
        setSelectedIndex(prev => 
          prev > 0 ? prev - 1 : suggestions.length - 1
        );
        break;
        
      case 'Enter':
        e.preventDefault();
        if (selectedIndex >= 0) {
          handleSelectSuggestion(suggestions[selectedIndex]);
        } else if (inputValue.trim()) {
          // Create new category from current input
          handleCreateFromInput();
        }
        break;
        
      case 'Escape':
        setIsOpen(false);
        setSelectedIndex(-1);
        inputRef.current?.blur();
        break;
    }
  };

  const handleSelectSuggestion = async (suggestion: CategorySuggestion) => {
    if (suggestion.isExisting && suggestion.id) {
      // Select existing category
      onChange(suggestion.id.toString());
      setInputValue(suggestion.fullPath);
      setIsOpen(false);
    } else if (suggestion.needsCreation) {
      // Create new category
      await handleCreateCategory(suggestion.fullPath);
    }
  };

  const handleCreateFromInput = async () => {
    if (inputValue.trim()) {
      await handleCreateCategory(inputValue.trim());
    }
  };

  const handleCreateCategory = async (categoryPath: string) => {
    setIsCreating(true);
    try {
      const newCategory = await onCreateCategory(categoryPath);
      if (newCategory) {
        onChange(newCategory.id.toString());
        setInputValue(categoryPath);
        setIsOpen(false);
      }
    } catch (error) {
      console.error('Failed to create category:', error);
    } finally {
      setIsCreating(false);
    }
  };

  // Find selected category to display
  const selectedCategory = categories.find(cat => cat.id.toString() === value);
  
  useEffect(() => {
    if (selectedCategory) {
      setInputValue(selectedCategory.fullPath || selectedCategory.name);
    }
  }, [selectedCategory]);

  return (
    <div className="relative" ref={dropdownRef}>
      <div className="relative">
        <Input
          ref={inputRef}
          type="text"
          value={inputValue}
          onChange={(e) => handleInputChange(e.target.value)}
          onKeyDown={handleKeyDown}
          onFocus={() => setIsOpen(true)}
          placeholder={placeholder}
          disabled={disabled || isCreating}
          className={`${selectedCategory?.color ? 'pr-16' : 'pr-10'} ${error ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
        />
        
        <div className="absolute inset-y-0 right-0 flex items-center pr-3 gap-2">
          {/* Selected category color indicator */}
          {selectedCategory?.color && (
            <div 
              className="w-4 h-4 rounded-full border border-gray-300" 
              style={{ backgroundColor: selectedCategory.color }}
            />
          )}
          
          {isCreating ? (
            <div className="w-4 h-4 border-2 border-primary-600 border-t-transparent rounded-full animate-spin" />
          ) : (
            <TagIcon className="w-4 h-4 text-gray-400" />
          )}
        </div>
      </div>

      {error && (
        <p className="mt-1 text-sm text-red-600">{error}</p>
      )}

      {isOpen && suggestions.length > 0 && (
        <div className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-64 overflow-y-auto">
          {suggestions.map((suggestion, index) => (
            <div
              key={`${suggestion.fullPath}-${index}`}
              onClick={() => handleSelectSuggestion(suggestion)}
              className={`px-4 py-2 cursor-pointer flex items-center gap-2 ${
                index === selectedIndex
                  ? 'bg-primary-50 text-primary-700'
                  : 'hover:bg-gray-50'
              }`}
            >
              {suggestion.isExisting ? (
                <CheckIcon className="w-4 h-4 text-green-500 flex-shrink-0" />
              ) : (
                <PlusIcon className="w-4 h-4 text-blue-500 flex-shrink-0" />
              )}
              
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  {/* Color indicator for existing categories */}
                  {suggestion.isExisting && suggestion.id && (() => {
                    const category = categories.find(c => c.id === suggestion.id);
                    const color = category?.color || '#6B7280';
                    return (
                      <div 
                        className="w-3 h-3 rounded-full flex-shrink-0" 
                        style={{ backgroundColor: color }}
                      />
                    );
                  })()}
                  
                  <div className="flex items-center gap-1">
                    {suggestion.fullPath.includes(' -> ') ? (
                      <>
                        {suggestion.fullPath.split(' -> ').map((part, partIndex, parts) => (
                          <span key={partIndex} className="flex items-center gap-1">
                            <span className={partIndex === parts.length - 1 ? 'font-medium' : 'text-gray-500'}>
                              {part}
                            </span>
                            {partIndex < parts.length - 1 && (
                              <ChevronRightIcon className="w-3 h-3 text-gray-400" />
                            )}
                          </span>
                        ))}
                      </>
                    ) : (
                      <span className="font-medium">{suggestion.name}</span>
                    )}
                  </div>
                </div>
                
                {!suggestion.isExisting && (
                  <div className="text-xs text-gray-500">
                    Press Enter to create
                  </div>
                )}
              </div>
            </div>
          ))}
          
          {inputValue.trim() && !suggestions.some(s => s.fullPath.toLowerCase() === inputValue.toLowerCase()) && (
            <div className="border-t border-gray-100 px-4 py-2 text-sm text-gray-500">
              Press Enter to create &quot;{inputValue}&quot;
            </div>
          )}
        </div>
      )}
    </div>
  );
}
