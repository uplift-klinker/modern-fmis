import { ThemeProvider } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { appTheme } from "@/shared/theme/theme";
import { ConfigProvider } from "@/shared/config/config-provider";
import { ConfiguredApp } from "@/app/configured-app";

export function App() {
  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <ConfigProvider>
        <ConfiguredApp />
      </ConfigProvider>
    </ThemeProvider>
  );
}
