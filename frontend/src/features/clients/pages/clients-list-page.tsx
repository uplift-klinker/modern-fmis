import { useState } from 'react';
import { Box, Button } from '@mui/material';
import { Outlet, useNavigate } from 'react-router-dom';
import { ClientList } from '@/features/clients/components/client-list';
import { CreateClientDialog } from '@/features/clients/dialogs/create-client-dialog';

export function ClientsListPage() {
  const navigate = useNavigate();
  const [createOpen, setCreateOpen] = useState(false);

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
        <Box sx={{ p: 1 }}>
          <Button variant="contained" onClick={() => setCreateOpen(true)}>
            New Client
          </Button>
        </Box>

        <ClientList onSelectClient={(clientId) => navigate(`/clients/${clientId}`)} />
      </Box>

      <Box sx={{ flex: 1, overflowY: 'auto' }}>
        <Outlet />
      </Box>

      <CreateClientDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(client) => {
          setCreateOpen(false);
          navigate(`/clients/${client.id}`);
        }}
      />
    </Box>
  );
}
