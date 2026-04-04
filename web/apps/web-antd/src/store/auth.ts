import type { UserInfo } from '@vben/types';

import { ref } from 'vue';

import { useAccessStore, useUserStore } from '@vben/stores';

import { defineStore } from 'pinia';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();

  const loginLoading = ref(false);

  async function fetchUserInfo() {
    const mockUser: UserInfo = {
      avatar: '',
      homePath: '/dashboard',
      realName: 'OpenStaff',
      roles: ['admin'],
      userId: '0',
      username: 'openstaff',
    };
    userStore.setUserInfo(mockUser);
    accessStore.setAccessCodes(['*']);
    return mockUser;
  }

  function $reset() {
    loginLoading.value = false;
  }

  return {
    $reset,
    fetchUserInfo,
    loginLoading,
  };
});
