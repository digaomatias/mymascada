'use client';

import { useState, useEffect, useRef } from 'react';
import { Input } from '@/components/ui/input';
import { apiClient } from '@/lib/api-client';
import { ChevronDownIcon } from '@heroicons/react/24/outline';

interface DescriptionAutocompleteProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  error?: string;
}

export default function DescriptionAutocomplete({
  value,
  onChange,
  placeholder = 'Type description...',
  disabled = false,
  error
}: DescriptionAutocompleteProps) {
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [loading, setLoading] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  
  const inputRef = useRef<HTMLInputElement>(null);
  const suggestionsRef = useRef<HTMLDivElement>(null);
  
  // Debounced search
  useEffect(() => {
    const timeoutId = setTimeout(async () => {
      if (value.length > 0) {
        await loadSuggestions(value);
      } else {
        // Show recent descriptions when field is focused but empty
        await loadSuggestions();
      }
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [value]);

  const loadSuggestions = async (searchTerm?: string) => {
    try {
      setLoading(true);
      const results = await apiClient.getDescriptionSuggestions(searchTerm, 10);
      setSuggestions(results || []);
    } catch (error) {
      console.error('Failed to load description suggestions:', error);
      setSuggestions([]);
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    onChange(newValue);
    setShowSuggestions(true);
    setHighlightedIndex(-1);
  };

  const handleSuggestionClick = (suggestion: string) => {
    onChange(suggestion);
    setShowSuggestions(false);
    setHighlightedIndex(-1);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!showSuggestions || suggestions.length === 0) return;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightedIndex(prev => 
          prev < suggestions.length - 1 ? prev + 1 : prev
        );
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightedIndex(prev => prev > 0 ? prev - 1 : -1);
        break;
      case 'Enter':
        e.preventDefault();
        if (highlightedIndex >= 0 && highlightedIndex < suggestions.length) {
          handleSuggestionClick(suggestions[highlightedIndex]);
        }
        break;
      case 'Escape':
        setShowSuggestions(false);
        setHighlightedIndex(-1);
        break;
    }
  };

  const handleFocus = () => {
    setShowSuggestions(true);
    if (suggestions.length === 0) {
      loadSuggestions(value || undefined);
    }
  };

  const handleBlur = () => {
    // Delay hiding suggestions to allow clicks on suggestions
    setTimeout(() => {
      if (suggestionsRef.current && !suggestionsRef.current.contains(document.activeElement)) {
        setShowSuggestions(false);
        setHighlightedIndex(-1);
      }
    }, 150);
  };

  return (
    <div className="relative">
      <div className="relative">
        <Input
          ref={inputRef}
          type="text"
          value={value}
          onChange={handleInputChange}
          onKeyDown={handleKeyDown}
          onFocus={handleFocus}
          onBlur={handleBlur}
          placeholder={placeholder}
          disabled={disabled}
          className={`pr-8 ${error ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
        />
        
        {/* Dropdown indicator */}
        <div className="absolute inset-y-0 right-0 flex items-center pr-3 pointer-events-none">
          <ChevronDownIcon 
            className={`w-4 h-4 text-gray-400 transition-transform ${
              showSuggestions ? 'rotate-180' : ''
            }`} 
          />
        </div>
      </div>

      {/* Suggestions dropdown */}
      {showSuggestions && (
        <div 
          ref={suggestionsRef}
          data-testid="description-suggestions"
          className="absolute z-50 w-full mt-1 bg-white border border-gray-300 rounded-md shadow-lg max-h-60 overflow-y-auto"
        >
          {loading ? (
            <div className="px-4 py-3 text-sm text-gray-500 text-center" data-testid="suggestions-loading">
              <div className="flex items-center justify-center gap-2">
                <div className="w-4 h-4 border-2 border-gray-300 border-t-primary-500 rounded-full animate-spin"></div>
                Loading suggestions...
              </div>
            </div>
          ) : suggestions.length > 0 ? (
            <div className="py-1">
              {suggestions.map((suggestion, index) => (
                <button
                  key={suggestion}
                  type="button"
                  data-testid="suggestion-item"
                  onClick={() => handleSuggestionClick(suggestion)}
                  className={`w-full text-left px-4 py-2 text-sm hover:bg-gray-100 focus:bg-gray-100 focus:outline-none ${
                    index === highlightedIndex ? 'bg-gray-100 highlighted' : ''
                  }`}
                  onMouseEnter={() => setHighlightedIndex(index)}
                  onMouseLeave={() => setHighlightedIndex(-1)}
                >
                  <span className="block truncate">{suggestion}</span>
                </button>
              ))}
            </div>
          ) : value.length > 0 ? (
            <div className="px-4 py-3 text-sm text-gray-500 text-center">
              No matching descriptions found
            </div>
          ) : (
            <div className="px-4 py-3 text-sm text-gray-500 text-center">
              Start typing to see suggestions
            </div>
          )}
        </div>
      )}

      {error && (
        <p className="mt-1 text-sm text-red-600">{error}</p>
      )}
    </div>
  );
}