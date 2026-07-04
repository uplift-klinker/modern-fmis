import { Box, Link, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

export function WelcomePage() {
  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        Welcome to modern-fmis
      </Typography>
      <Link component={RouterLink} to="/clients">
        Go to clients
      </Link>
    </Box>
  );
}
