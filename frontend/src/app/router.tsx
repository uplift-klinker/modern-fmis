import { Navigate, Route, createBrowserRouter, createRoutesFromElements } from 'react-router-dom';
import { AppLayout } from '@/app/AppLayout';
import { RequireAuth } from '@/routes/RequireAuth';
import { WelcomePage } from '@/routes/WelcomePage';
import { UnauthorizedPage } from '@/routes/UnauthorizedPage';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';
import { ClientDetailPage } from '@/features/clients/pages/ClientDetailPage';

export const appRoutes = (
  <>
    <Route path="/unauthorized" element={<UnauthorizedPage />} />
    <Route element={<RequireAuth />}>
      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to="/welcome" replace />} />
        <Route path="/welcome" element={<WelcomePage />} />
        <Route path="/clients" element={<ClientsListPage />}>
          <Route path=":id" element={<ClientDetailPage />} />
        </Route>
      </Route>
    </Route>
  </>
);

export const router = createBrowserRouter(createRoutesFromElements(appRoutes));
