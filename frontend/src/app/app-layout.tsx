import { useState, type MouseEvent } from "react";
import {
  AppBar,
  Avatar,
  Box,
  IconButton,
  Menu,
  MenuItem,
  Toolbar,
  Typography,
} from "@mui/material";
import { Outlet } from "react-router-dom";
import { useAuth } from "@/shared/auth/auth-context";

export function AppLayout() {
  const auth = useAuth();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);

  const openMenu = (event: MouseEvent<HTMLElement>) => setAnchorEl(event.currentTarget);
  const closeMenu = () => setAnchorEl(null);
  const logout = () => {
    closeMenu();
    auth.logout();
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100vh" }}>
      <AppBar position="static">
        <Toolbar sx={{ justifyContent: "space-between" }}>
          <Typography variant="h6">modern-fmis</Typography>
          {auth.isAuthenticated && (
            <>
              <IconButton
                aria-label="Account menu"
                aria-haspopup="menu"
                color="inherit"
                onClick={openMenu}
              >
                <Avatar sx={{ width: 32, height: 32 }} />
              </IconButton>
              <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={closeMenu}>
                <MenuItem onClick={logout}>Log out</MenuItem>
              </Menu>
            </>
          )}
        </Toolbar>
      </AppBar>
      <Box
        component="main"
        sx={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column", p: 2 }}
      >
        <Outlet />
      </Box>
    </Box>
  );
}
