import { Box } from '@mui/material';
import { Outlet, useNavigate } from 'react-router-dom';
import { ClientList } from '@/features/clients/components/ClientList';

export function ClientsListPage() {
  const navigate = useNavigate();

  return (
    <Box sx={{ display: 'flex', height: '100%' }}>
      <Box
        sx={{
          width: 320,
          flexShrink: 0,
          overflowY: 'auto',
          borderRight: 1,
          borderColor: 'divider',
        }}
      >
        <ClientList onSelectClient={(clientId) => navigate(`/clients/${clientId}`)} />
      </Box>
      <Box sx={{ flex: 1, overflowY: 'auto' }}>
        <Outlet />
      </Box>
    </Box>
  );
}
