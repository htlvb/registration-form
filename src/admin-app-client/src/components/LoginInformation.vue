<script setup lang="ts">
import { login, tryGetLoggedInUser } from '@/auth'
import { ref } from 'vue'

const username = ref<string>()
const setLoggedInUser = async () => {
  let user = await tryGetLoggedInUser()
  if (user === undefined) {
    await login([])
    user = await tryGetLoggedInUser()
  }
  if (user === undefined) {
    return
  }
  username.value = user
}
setLoggedInUser()
</script>

<template>
  <h2 class="text-lg">
    <span v-if="username !== undefined">Angemeldet als <span class="font-bold">{{ username }}</span></span>
  </h2>
</template>
