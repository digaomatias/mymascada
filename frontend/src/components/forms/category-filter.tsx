'use client';

import { useState, useMemo, useRef, useEffect } from 'react';
import { Input } from '@/components/ui/input';
import { useTranslations } from 'next-intl';
import { 
  MagnifyingGlassIcon,
  FolderIcon,
  XMarkIcon,
  ChevronDownIcon
} from '@heroicons/react/24/outline';

interface Category {
  id: number;
  name: string;
  type: number;
  parentId: number | null;
  fullPath?: string;
}

interface CategoryFilterProps {
  value?: string;
  onChange: (categoryId: string) => void;
  categories: Category[];
  placeholder?: string;
}

export function CategoryFilter({
  value,
  onChange,
  categories,
  placeholder
}: CategoryFilterProps) {
  const t = useTranslations('categories');
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const resolvedPlaceholder = placeholder || t('filter.allCategories');

  // Get selected category
  const selectedCategory = (categories || []).find(cat => cat?.id === Number(value));

  // Get search results
  const searchResults = useMemo(() => {
    if (!searchTerm) return categories || [];
    
    const searchLower = searchTerm.toLowerCase();
    return (categories || [])
      .filter(cat => cat?.name?.toLowerCase?.()?.includes(searchLower))
      .slice(0, 20);
  }, [categories, searchTerm]);

  const handleSelect = (categoryId: number) => {
    onChange(categoryId.toString());
    setIsOpen(false);
    setSearchTerm('');
  };

  const handleClear = () => {
    onChange('');
    setIsOpen(false);
    setSearchTerm('');
  };

  const handleInputClick = () => {
    setIsOpen(!isOpen);
    if (!isOpen) {
      setSearchTerm('');
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchTerm(e.target.value);
    setIsOpen(true);
  };

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (inputRef.current && !inputRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSearchTerm('');
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className="relative" ref={inputRef}>
      <div className="relative">
        <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
        <Input
          type="text"
          placeholder={selectedCategory ? selectedCategory.name : resolvedPlaceholder}
          value={isOpen ? searchTerm : (selectedCategory ? selectedCategory.name : '')}
          onChange={handleInputChange}
          onClick={handleInputClick}
          className="pl-10 pr-20"
        />
        <div className="absolute right-3 top-1/2 transform -translate-y-1/2 flex items-center gap-1">
          {selectedCategory && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                handleClear();
              }}
              className="p-1 hover:bg-gray-100 rounded cursor-pointer"
              title={t('clearFilter')}
            >
              <XMarkIcon className="w-4 h-4 text-gray-400 hover:text-gray-600" />
            </button>
          )}
          <ChevronDownIcon className="w-4 h-4 text-gray-400" />
        </div>
      </div>

      {isOpen && (
        <div className="absolute z-20 mt-1 w-full bg-white rounded-lg shadow-lg border border-gray-200 max-h-60 overflow-y-auto">
          {searchResults.length > 0 ? (
            <div className="p-2">
              {searchResults.map((cat) => (
                <button
                  key={cat.id}
                  type="button"
                  onClick={() => handleSelect(cat.id)}
                  className={`
                    w-full flex items-center gap-3 px-3 py-2 rounded-md text-left transition-colors
                    ${cat.id === Number(value) ? 'bg-primary-100 text-primary-700' : 'hover:bg-gray-50'}
                  `}
                >
                  <FolderIcon className="w-4 h-4 text-gray-400" />
                  <span className="text-sm">{cat.name}</span>
                </button>
              ))}
            </div>
          ) : (
            <div className="p-4 text-center">
              <p className="text-gray-500 text-sm">{t('filter.noCategoriesFound')}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
