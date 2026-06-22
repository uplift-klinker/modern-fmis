import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material';
import { Outlet } from 'react-router-dom';
import { useAuth } from '@/shared/auth/auth-context';

export function AppLayout() {
  const auth = useAuth();

  return (
    <Box>
      <AppBar position="static">
        <Toolbar sx={{ justifyContent: 'space-between' }}>
          <Typography variant="h6">modern-fmis</Typography>
          {auth.isAuthenticated && (
            <Button color="inherit" onClick={auth.logout}>
              {auth.userEmail ?? 'Log out'}
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <Container sx={{ py: 3 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
