<script setup lang="ts">
import { ref } from 'vue'
import LoginInformation from './components/LoginInformation.vue'
import LoadingBar from './components/LoadingBar.vue'
import ErrorWithRetry from './components/ErrorWithRetry.vue'
import { uiFetchAuthorized } from './UIFetch'
import type { Dto } from './DataTransfer'
import EventList from './EventList.vue'

const isLoadingEvents = ref(false)
const hasLoadingEventsFailed = ref(false)
const events = ref<Dto.Event[]>()
const loadEvents = async () => {
  const result = await uiFetchAuthorized(isLoadingEvents, hasLoadingEventsFailed, "/api/events")
  if (result.succeeded) {
    events.value = await result.response.json() as Dto.Event[]
  }
}
loadEvents()
</script>

<template>
  <header class="bg-blue-htlvb">
    <div class="container mx-auto flex flex-col sm:flex-row gap-2 sm:gap-6 my-4 px-4">
      <div>
        <img src="@/assets/logo.svg" class="h-[80px]" />
      </div>
      <div class="grow flex flex-col gap-2 text-slate-300">
        <span class="text-2xl small-caps">Eventregistrierung</span>
        <span class="text-4xl small-caps">Administration</span>
      </div>
      <LoginInformation class="self-end" />
    </div>
  </header>

  <main class="container mx-auto">
    <section class="flex flex-col gap-4 mx-4 my-2">
      <LoadingBar v-if="isLoadingEvents"></LoadingBar>
      <ErrorWithRetry v-else-if="hasLoadingEventsFailed" @retry="loadEvents">Fehler beim Laden der Events.</ErrorWithRetry>
      <template v-else-if="events !== undefined">
        <section class="border rounded px-4 py-2">
          <h2 class="text-2xl">Entwürfe</h2>
          <EventList :events="events.filter(v => v.type === 'draft')" />
        </section>
        <section class="border rounded px-4 py-2">
          <h2 class="text-2xl">Aktive Events</h2>
          <EventList :events="events.filter(v => v.type === 'released')" />
        </section>
        <section class="border rounded px-4 py-2">
          <h2 class="text-2xl">Archiv</h2>
          <EventList :events="events.filter(v => v.type === 'archived')" />
        </section>
      </template>
    </section>
  </main>
</template>
