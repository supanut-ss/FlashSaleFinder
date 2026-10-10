import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "@fontsource-variable/plus-jakarta-sans";
import "@fontsource-variable/noto-sans-thai";
import "./styles.css";
import App from "./App";
import { SessionProvider } from "./lib/session";

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <SessionProvider>
      <App />
    </SessionProvider>
  </StrictMode>,
);
