import nextConfig from "eslint-config-next";
import coreWebVitals from "eslint-config-next/core-web-vitals";
import typescript from "eslint-config-next/typescript";

/** @type {import("eslint").Linter.Config[]} */
const eslintConfig = [
  {
    ignores: [
      "**/__tests__/**",
      "**/*.test.ts",
      "**/*.test.tsx",
      "**/*.spec.ts",
      "**/*.spec.tsx",
      "test-setup.ts",
      "vitest.config.ts",
      ".next/",
      "out/",
      "node_modules/",
      ".env*",
      "*.log",
      "public/**",
      "tests/**",
    ],
  },
  ...nextConfig,
  ...coreWebVitals,
  ...typescript,
  {
    // CommonJS config files at project root
    files: ["*.js"],
    rules: {
      "@typescript-eslint/no-require-imports": "off",
    },
  },
  {
    rules: {
      "@typescript-eslint/no-unused-vars": "error",
      "@typescript-eslint/no-explicit-any": "warn",
      "prefer-const": "error",
      // Downgrade new react-hooks v7 rules to warnings for now.
      // These flag pre-existing patterns that should be cleaned up separately.
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/static-components": "warn",
      "react-hooks/preserve-manual-memoization": "warn",
    },
  },
];

export default eslintConfig;
