<script setup lang="ts">
import { ref } from 'vue'
import { uiFetch } from './UIFetch'
import type { Schedule } from './DataTransfer'
import LoadingBar from './components/LoadingBar.vue'
import ErrorWithRetry from './components/ErrorWithRetry.vue'
import RegistrationForm from './components/RegistrationForm.vue'
import { DateTime } from './Utils'

const schedule = ref<Schedule>()
const isLoadingSchedule = ref(false)
const hasLoadingScheduleFailed = ref(false)
const loadSchedule = async () => {
  const eventKey = location.pathname.split('/').pop()
  const result = await uiFetch(isLoadingSchedule, hasLoadingScheduleFailed, `/api/schedule/${eventKey}`)
  if (result.succeeded) {
    schedule.value = await result.response.json() as Schedule
  }
}
loadSchedule()
</script>

<template>
  <header class="bg-blue-htlvb">
    <div class="max-w-screen-lg mx-auto flex gap-6">
      <img src="@/assets/logo.svg" class="h-[80px] my-4" />
      <div class="flex flex-col gap-2 py-4 text-slate-300">
        <span class="text-2xl small-caps">Registrierung</span>
        <span v-if="schedule !== undefined" class="text-4xl small-caps">{{ schedule.title }}</span>
      </div>
    </div>
  </header>
  <main class="grow overflow-y-scroll">
    <div class="max-w-screen-lg mx-auto">
      <LoadingBar v-if="isLoadingSchedule" class="m-4" />
      <ErrorWithRetry v-else-if="hasLoadingScheduleFailed" @retry="loadSchedule" class="m-4">Fehler beim Laden des Formulars.</ErrorWithRetry>
      <ErrorWithRetry v-else-if="schedule?.type === 'hidden'" @retry="loadSchedule" class="m-4">Die Registrierung ist ab {{ DateTime.format(new Date(schedule.reservationStartTime), { weekday: 'long' }) }} Uhr möglich.</ErrorWithRetry>
      <RegistrationForm v-else-if="schedule?.type === 'released'" :schedule="schedule" />
    </div>
  </main>
</template>
