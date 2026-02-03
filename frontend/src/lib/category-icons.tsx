import {
  BanknotesIcon,
  BriefcaseIcon,
  HomeIcon,
  ShoppingBagIcon,
  ShoppingCartIcon,
  TruckIcon,
  FilmIcon,
  HeartIcon,
  AcademicCapIcon,
  GiftIcon,
  WrenchScrewdriverIcon,
  DevicePhoneMobileIcon,
  ComputerDesktopIcon,
  FaceSmileIcon,
  BeakerIcon,
  CreditCardIcon,
  ChartBarIcon,
  BuildingOffice2Icon,
  CurrencyDollarIcon,
  LightBulbIcon,
  PhoneIcon,
  TvIcon,
  MusicalNoteIcon,
  BookOpenIcon,
  PaintBrushIcon,
  CakeIcon,
  TagIcon,
  FireIcon,
  ReceiptPercentIcon,
  ArrowsRightLeftIcon,
  ClockIcon,
  CalendarIcon,
  MapPinIcon,
  GlobeAltIcon,
  CameraIcon,
  SunIcon,
  StarIcon,
  ClipboardDocumentListIcon
} from '@heroicons/react/24/outline';
import React from 'react';

// Map category icons to Heroicon components
export const categoryIconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  // Income categories
  'money-bag': BanknotesIcon,
  'briefcase': BriefcaseIcon,
  'currency-dollar': CurrencyDollarIcon,
  'chart-bar': ChartBarIcon,
  'building-office': BuildingOffice2Icon,
  'gift': GiftIcon,
  'star': StarIcon,

  // Housing & Utilities
  'home': HomeIcon,
  'light-bulb': LightBulbIcon,
  'fire': FireIcon,
  'phone': PhoneIcon,
  'globe': GlobeAltIcon,

  // Transportation
  'car': TruckIcon,
  'truck': TruckIcon,
  'fuel': FireIcon,
  'map-pin': MapPinIcon,

  // Food & Dining
  'shopping-cart': ShoppingCartIcon,
  'shopping-bag': ShoppingBagIcon,
  'cake': CakeIcon,

  // Shopping
  'computer': ComputerDesktopIcon,
  'phone-mobile': DevicePhoneMobileIcon,
  'camera': CameraIcon,
  'book': BookOpenIcon,

  // Entertainment
  'film': FilmIcon,
  'tv': TvIcon,
  'musical-note': MusicalNoteIcon,
  'paint-brush': PaintBrushIcon,

  // Health & Medical
  'heart': HeartIcon,
  'medical': BeakerIcon,
  'academic-cap': AcademicCapIcon,
  'face-smile': FaceSmileIcon,

  // Business & Professional
  'wrench': WrenchScrewdriverIcon,
  'clipboard': ClipboardDocumentListIcon,
  'receipt': ReceiptPercentIcon,

  // Financial
  'credit-card': CreditCardIcon,
  'bank': BanknotesIcon,
  'arrows-exchange': ArrowsRightLeftIcon,

  // Time & Events
  'clock': ClockIcon,
  'calendar': CalendarIcon,
  'sun': SunIcon,

  // Default fallback
  'tag': TagIcon,
};

// Emoji to Heroicon mapping for backend compatibility
const emojiToIconMap: Record<string, string> = {
  '💰': 'money-bag',
  '💼': 'briefcase', 
  '🏠': 'home',
  '🛒': 'shopping-cart',
  '🛍️': 'shopping-bag',
  '🚗': 'car',
  '⛽': 'fuel',
  '🎬': 'film',
  '📺': 'tv',
  '🎮': 'gamepad',
  '🎵': 'musical-note',
  '🏥': 'heart',
  '💊': 'medical',
  '🎓': 'academic-cap',
  '🎁': 'gift',
  '🔧': 'wrench',
  '📱': 'phone-mobile',
  '💻': 'computer',
  '👕': 'shirt',
  '😊': 'face-smile',
  '☕': 'coffee',
  '🍰': 'cake',
  '🍕': 'pizza',
  '💳': 'credit-card',
  '💡': 'light-bulb',
  '☎️': 'phone',
  '📸': 'camera',
  '📖': 'book',
  '🎨': 'paint-brush',
  '✈️': 'plane',
  '📊': 'chart-bar',
  '🏢': 'building-office',
  '💵': 'currency-dollar',
  '🔥': 'fire',
  '🌍': 'globe',
  '📍': 'map-pin',
  '⏰': 'clock',
  '📅': 'calendar',
  '☀️': 'sun',
  '⭐': 'star',
  '📋': 'clipboard',
  '🧾': 'receipt',
  '🔄': 'arrows-exchange',
  '🏦': 'bank',
};

// Function to get icon component by name (handles both emojis and icon names)
export function getCategoryIcon(iconName?: string): React.ComponentType<{ className?: string }> {
  if (!iconName) return TagIcon;
  
  // Check if it's an emoji and convert to icon name
  const mappedIconName = emojiToIconMap[iconName] || iconName;
  
  return categoryIconMap[mappedIconName] || TagIcon;
}

// Function to render icon component
export function renderCategoryIcon(iconName?: string, className?: string) {
  const IconComponent = getCategoryIcon(iconName);
  return <IconComponent className={className} />;
}

// Available icon options for category creation
export const availableIcons = [
  { name: 'money-bag', label: 'Money', component: BanknotesIcon },
  { name: 'briefcase', label: 'Briefcase', component: BriefcaseIcon },
  { name: 'home', label: 'Home', component: HomeIcon },
  { name: 'shopping-cart', label: 'Shopping Cart', component: ShoppingCartIcon },
  { name: 'shopping-bag', label: 'Shopping Bag', component: ShoppingBagIcon },
  { name: 'car', label: 'Transportation', component: TruckIcon },
  { name: 'film', label: 'Entertainment', component: FilmIcon },
  { name: 'heart', label: 'Health', component: HeartIcon },
  { name: 'academic-cap', label: 'Education', component: AcademicCapIcon },
  { name: 'gift', label: 'Gifts', component: GiftIcon },
  { name: 'wrench', label: 'Services', component: WrenchScrewdriverIcon },
  { name: 'phone-mobile', label: 'Electronics', component: DevicePhoneMobileIcon },
  { name: 'credit-card', label: 'Financial', component: CreditCardIcon },
  { name: 'light-bulb', label: 'Utilities', component: LightBulbIcon },
  { name: 'fuel', label: 'Fuel', component: FireIcon },
  { name: 'tv', label: 'Media', component: TvIcon },
  { name: 'musical-note', label: 'Music', component: MusicalNoteIcon },
  { name: 'book', label: 'Books', component: BookOpenIcon },
  { name: 'paint-brush', label: 'Arts & Crafts', component: PaintBrushIcon },
  { name: 'camera', label: 'Photography', component: CameraIcon },
  { name: 'chart-bar', label: 'Business', component: ChartBarIcon },
  { name: 'tag', label: 'General', component: TagIcon },
];