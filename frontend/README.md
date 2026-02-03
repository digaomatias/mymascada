# MyMascada - Personal Finance Management

A beautiful, modern personal finance management application built with Next.js, TypeScript, and Tailwind CSS. Features a clean purple gradient theme, professional UI components, and comprehensive financial tracking capabilities.

![MyMascada Dashboard](https://img.shields.io/badge/version-1.0.0-purple.svg)
![Next.js](https://img.shields.io/badge/Next.js-15-black)
![TypeScript](https://img.shields.io/badge/TypeScript-5.8-blue)
![Tailwind CSS](https://img.shields.io/badge/Tailwind-3.4-38B2AC)

## ✨ Features

### 💰 Financial Management
- **Transaction Tracking** - Record and categorize all your income and expenses
- **Smart Categories** - Hierarchical category system with beautiful Heroicon integration
- **Account Management** - Track multiple accounts (checking, savings, credit cards)
- **Dashboard Overview** - See your financial health at a glance
- **Monthly Summary** - Income vs expenses breakdown with trends

### 🎨 Beautiful UI/UX
- **Purple Gradient Theme** - Modern glass-morphism design throughout
- **Custom Date Picker** - Beautiful calendar component with smooth animations
- **Responsive Design** - Works perfectly on desktop, tablet, and mobile
- **Professional Icons** - Consistent Heroicon usage (no emojis!)
- **Dark Mode Ready** - Infrastructure in place for dark theme

### 📱 Technical Features
- **Progressive Web App** - Install on mobile for native app experience
- **Type-Safe** - Full TypeScript implementation
- **Fast Performance** - Next.js 15 with Turbopack
- **Authentication** - Secure JWT-based authentication
- **API Integration** - Clean architecture with dedicated API client

## 🚀 Getting Started

### Prerequisites
- Node.js 20+ (recommended: use nvm)
- Backend API running (default: http://localhost:5125)

### Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/mymascada.git
cd mymascada/frontend

# Install dependencies
npm install

# Start development server
npm run dev

# Open http://localhost:3000
```

### Environment Configuration

Create a `.env.local` file:

```env
NEXT_PUBLIC_API_URL=http://localhost:5125
```

## 📁 Project Structure

```
src/
├── app/                    # Next.js app router pages
│   ├── dashboard/         # Financial overview
│   ├── transactions/      # Transaction management
│   ├── accounts/          # Account management
│   ├── categories/        # Category management
│   └── auth/             # Authentication pages
├── components/            # Reusable components
│   ├── ui/               # Base UI components
│   ├── forms/            # Form components
│   ├── modals/           # Modal components
│   └── navigation.tsx    # Main navigation
├── contexts/             # React contexts
│   └── auth-context.tsx  # Authentication state
├── lib/                  # Utilities
│   ├── api-client.ts    # API communication
│   ├── utils.ts         # Helper functions
│   └── category-icons.tsx # Icon mappings
├── hooks/                # Custom React hooks
└── types/               # TypeScript definitions
```

## 🛠️ Available Scripts

```bash
npm run dev          # Start development server with hot reload
npm run build        # Build for production
npm run start        # Start production server
npm run lint         # Run ESLint
npm run type-check   # Run TypeScript compiler
npm test            # Run tests (when configured)
```

## 🎯 Key Components

### Custom Date Picker
A beautiful date picker component that matches the app's design:
- Purple gradient header
- Smooth animations
- Mobile responsive
- No time picker for simplicity

### Category System
Professional category management with:
- Heroicon integration (no emojis!)
- Hierarchical structure
- Color coding
- Smart search

### Transaction Forms
Intuitive transaction entry with:
- Smart category suggestions
- Account selection
- Optional notes and location
- Responsive design

## 🐳 Docker Deployment

```bash
# Build Docker image
docker build -t mymascada-frontend .

# Run container
docker run -p 3000:3000 mymascada-frontend

# Or use docker-compose (from project root)
docker-compose up frontend
```

## 🔧 Configuration

### API Integration
The app expects a backend API at `http://localhost:5125` by default. Configure via:
- Development: `.env.local`
- Production: Environment variables

### PWA Configuration
Progressive Web App features are configured in:
- `public/manifest.json` - App metadata
- `next.config.js` - PWA plugin settings

## 🤝 Contributing

This is currently a personal project, but contributions are welcome!

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is private and proprietary. All rights reserved.

## 🙏 Acknowledgments

- Built with [Next.js](https://nextjs.org/)
- Styled with [Tailwind CSS](https://tailwindcss.com/)
- Icons from [Heroicons](https://heroicons.com/)
- Date picker using [React Day Picker](https://react-day-picker.js.org/)

---

**MyMascada** - Taking control of your financial future, one transaction at a time. 💜