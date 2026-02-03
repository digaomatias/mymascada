import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      zIndex: {
        '10': '10', // Dropdowns
        '20': '20', // Modals, backdrops
        '30': '30', // Popovers, date-time pickers
        '40': '40', // Tooltips
        '50': '50', // Navigation
      },
    },
  },
  plugins: [],
}
export default config
