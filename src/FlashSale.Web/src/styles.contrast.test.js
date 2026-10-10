import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// Guards the design tokens: every text/background pair must keep WCAG AA in both themes.
const css = readFileSync(new URL("./styles.css", import.meta.url), "utf8");

const block = source => Object.fromEntries([...source.matchAll(/--([a-z0-9-]+):\s*([^;]+);/g)].map(m => [m[1], m[2].trim()]));
const light = block(css.match(/:root\s*{([^}]*)}/)[1]);
const darkOverrides = block(css.match(/prefers-color-scheme:\s*dark\)\s*{\s*:root\s*{([^}]*)}/)[1]);

const resolve = (tokens, name) => {
  let value = tokens[name];
  const alias = value?.match(/^var\(--([a-z0-9-]+)\)$/);
  return alias ? resolve(tokens, alias[1]) : value;
};
const themes = { light, dark: { ...light, ...darkOverrides } };

const luminance = hex => {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map(c => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const ratio = (a, b) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};

// [foreground, background, minimum ratio] — 4.5 for text, 3 for borders and focus rings.
const PAIRS = [
  ["ink", "paper", 4.5], ["ink", "surface", 4.5], ["ink", "hover", 4.5], ["ink", "chip", 4.5],
  ["ink-2", "paper", 4.5], ["ink-2", "surface", 4.5], ["ink-2", "hover", 4.5], ["ink-2", "chip", 4.5],
  ["below", "below-bg", 4.5], ["above", "above-bg", 4.5],
  ["on-link", "link", 4.5], ["link", "paper", 4.5], ["link", "surface", 4.5],
  ["on-tag", "tag", 4.5],
  ["ink-2", "surface", 3],
];

describe.each(Object.keys(themes))("%s theme contrast", name => {
  it.each(PAIRS)("%s on %s is at least %s:1", (fg, bg, min) => {
    const tokens = themes[name];
    expect(ratio(resolve(tokens, fg), resolve(tokens, bg))).toBeGreaterThanOrEqual(min);
  });
});
