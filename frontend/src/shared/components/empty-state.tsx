import { Box, Button, Typography } from "@mui/material";

export interface EmptyStateProps {
  message: string;
  onRefresh: () => void;
}

export function EmptyState({ message, onRefresh }: EmptyStateProps) {
  return (
    <Box sx={{ p: 3, textAlign: "center" }}>
      <Typography color="text.secondary" gutterBottom>
        {message}
      </Typography>
      <Button onClick={onRefresh}>Refresh</Button>
    </Box>
  );
}
