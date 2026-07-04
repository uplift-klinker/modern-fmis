import { createSlice, type PayloadAction } from "@reduxjs/toolkit";

interface AuthSliceState {
  accessToken: string | null;
}

const initialState: AuthSliceState = { accessToken: null };

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setAccessToken: (state, action: PayloadAction<string | null>) => {
      state.accessToken = action.payload;
    },
  },
});

export const { setAccessToken } = authSlice.actions;
export const authReducer = authSlice.reducer;
