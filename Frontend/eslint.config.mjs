import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
  {
    rules: {
      // Over-fires on intentional, SSR-safe patterns: synchronous localStorage
      // hydration on mount (auth/consent providers) and form-state reset on
      // dialog open. These are not bugs — keep them visible as warnings rather
      // than failing CI or refactoring working effects.
      "react-hooks/set-state-in-effect": "warn",
    },
  },
]);

export default eslintConfig;
