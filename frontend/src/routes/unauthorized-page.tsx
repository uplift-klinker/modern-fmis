import { Box, Typography } from '@mui/material';

export function UnauthorizedPage() {
  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        Unauthorized
      </Typography>
      <Typography>We couldn't sign you in. Please contact your administrator.</Typography>
    </Box>
  );
}
