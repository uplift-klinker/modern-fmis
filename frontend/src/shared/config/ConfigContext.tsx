import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import { CircularProgress } from '@mui/material';
import { loadAppConfig, type AppConfig } from '@/shared/config/appConfig';

const ConfigContext = createContext<AppConfig | null>(null);

export function ConfigProvider({
  config,
  children,
}: {
  config?: AppConfig;
  children: ReactNode;
}) {
  const [loaded, setLoaded] = useState<AppConfig | null>(config ?? null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (config) return;
    let active = true;
    loadAppConfig().then(
      (value) => active && setLoaded(value),
      () => active && setFailed(true),
    );
    return () => {
      active = false;
    };
  }, [config]);

  if (failed)
    return <div role="alert">Failed to load application configuration.</div>;
  if (!loaded) return <CircularProgress aria-label="Loading configuration" />;
  return (
    <ConfigContext.Provider value={loaded}>{children}</ConfigContext.Provider>
  );
}

export function useConfig(): AppConfig {
  const config = useContext(ConfigContext);
  if (!config) {
    throw new Error('useConfig must be used within a ConfigProvider');
  }
  return config;
}
