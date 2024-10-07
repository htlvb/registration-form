<script setup lang="ts">
import { computed, ref } from 'vue'
import { uiFetch } from './UIFetch'
import type { Event } from './DataTransfer'
import LoadingBar from './components/LoadingBar.vue'
import ErrorWithRetry from './components/ErrorWithRetry.vue'
import RegistrationForm from './components/RegistrationForm.vue'
import { DateTime } from './Utils'

const event = ref<Event>()
const isLoadingEvent = ref(false)
const hasLoadingEventFailed = ref(false)
const loadEvent = async () => {
  const eventKey = location.pathname.split('/').pop()
  const result = await uiFetch(isLoadingEvent, hasLoadingEventFailed, `/api/event/${eventKey}`)
  if (result.succeeded) {
    event.value = await result.response.json() as Event
  }
}
loadEvent()

const reservationStartTime = computed(() => {
  if (event.value?.type !== 'hidden') return undefined

  const time = new Date(event.value.reservationStartTime)
  if (time.getHours() === 0 && time.getMinutes() === 0 && time.getSeconds() === 0 && time.getMilliseconds() === 0) {
    return DateTime.formatDate(time, { weekday: 'long' })
  }
  return DateTime.format(time, { weekday: 'long' })
})
</script>

<template>
  <header class="bg-blue-htlvb">
    <div class="max-w-screen-lg mx-auto flex gap-6">
      <img src="@/assets/logo.svg" class="h-[80px] my-4" />
      <div class="flex flex-col gap-2 py-4 text-slate-300">
        <span class="text-2xl small-caps">Registrierung</span>
        <span v-if="event !== undefined" class="text-4xl small-caps">{{ event.title }}</span>
      </div>
    </div>
  </header>
  <main class="grow overflow-y-scroll">
    <div class="max-w-screen-lg mx-auto">
      <LoadingBar v-if="isLoadingEvent" class="m-4" />
      <ErrorWithRetry v-else-if="hasLoadingEventFailed" @retry="loadEvent" class="m-4">Fehler beim Laden des Formulars.</ErrorWithRetry>
      <ErrorWithRetry v-else-if="event?.type === 'hidden'" @retry="loadEvent" class="m-4">Die Registrierung ist ab {{ reservationStartTime }} Uhr möglich.</ErrorWithRetry>
      <RegistrationForm v-else-if="event?.type === 'released'" :event="event" />
    </div>
  </main>
</template>
