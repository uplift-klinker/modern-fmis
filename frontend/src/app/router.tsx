import { Navigate, Route, createBrowserRouter, createRoutesFromElements } from 'react-router-dom';
import { AppLayout } from '@/app/app-layout';
import { RequireAuth } from '@/routes/require-auth';
import { WelcomePage } from '@/routes/welcome-page';
import { UnauthorizedPage } from '@/routes/unauthorized-page';
import { ClientsListPage } from '@/features/clients/pages/clients-list-page';
import { ClientDetailPage } from '@/features/clients/pages/client-detail-page';

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
